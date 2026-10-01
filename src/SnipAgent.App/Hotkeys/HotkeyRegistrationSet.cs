using System.IO;
using System.Text.Json;
using SnipAgent.App.Models;

namespace SnipAgent.App.Hotkeys;

public sealed record HotkeyApplyResult(bool Success, IReadOnlyList<string> Errors);

internal sealed class HotkeyRegistrationSet(
    Func<int, HotkeyDefinition, bool> register,
    Func<int, bool> unregister)
{
    private readonly Dictionary<int, HotkeyBinding> _active = new();

    public HotkeyBinding? FindBinding(int id) => _active.GetValueOrDefault(id);

    public IReadOnlyList<HotkeyFailure> RegisterAll(IReadOnlyList<HotkeyBinding> bindings)
    {
        var failures = new List<HotkeyFailure>();
        foreach (var binding in bindings)
        {
            if (!binding.Hotkey.IsValid())
            {
                failures.Add(new(binding, "Invalid key combination. Change it in Settings."));
            }
            else if (_active.Values.Any(active => active.Hotkey.HasSameCombination(binding.Hotkey)))
            {
                var owner = _active.Values.First(active => active.Hotkey.HasSameCombination(binding.Hotkey));
                failures.Add(new(binding, $"The combination is already assigned to {owner.Name}."));
            }
            else if (!TryRegister(binding))
            {
                failures.Add(new(binding, "The combination is unavailable in Windows or another app."));
            }
        }

        return failures;
    }

    public HotkeyApplyResult TryApply(IReadOnlyList<HotkeyBinding> bindings, Action persist)
    {
        var errors = HotkeyBindings.Validate(bindings).Select(failure => failure.ToString()).ToList();
        if (errors.Count > 0)
        {
            return new(false, errors);
        }

        var previous = _active.Values.ToArray();
        foreach (var old in previous)
        {
            if (bindings.Any(binding => binding.Id == old.Id && binding.Hotkey.HasSameCombination(old.Hotkey)))
            {
                continue;
            }

            if (!TryUnregister(old))
            {
                errors.Add($"{old}: The previous shortcut could not be released.");
                Restore(previous, errors);
                return new(false, errors);
            }
        }

        foreach (var binding in bindings)
        {
            if (_active.ContainsKey(binding.Id))
            {
                continue;
            }

            if (!TryRegister(binding))
            {
                errors.Add($"{binding}: The combination is unavailable in Windows or another app.");
            }
        }

        if (errors.Count == 0)
        {
            try
            {
                persist();
                return new(true, Array.Empty<string>());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                errors.Add($"Settings could not be saved: {ex.Message}");
            }
        }

        Restore(previous, errors);
        return new(false, errors);
    }

    public IReadOnlyList<string> UnregisterAll()
    {
        var errors = new List<string>();
        foreach (var binding in _active.Values.ToArray())
        {
            if (!TryUnregister(binding))
            {
                errors.Add($"{binding}: The shortcut could not be released.");
            }
        }

        return errors;
    }

    private void Restore(IReadOnlyList<HotkeyBinding> previous, List<string> errors)
    {
        foreach (var current in _active.Values.ToArray())
        {
            if (previous.Any(old => old.Id == current.Id && old.Hotkey.HasSameCombination(current.Hotkey)))
            {
                continue;
            }

            if (!TryUnregister(current))
            {
                errors.Add($"{current}: The pending shortcut could not be released during rollback.");
            }
        }

        foreach (var old in previous)
        {
            if (_active.TryGetValue(old.Id, out var current))
            {
                if (!current.Hotkey.HasSameCombination(old.Hotkey))
                {
                    errors.Add($"{old}: The previous shortcut is inactive because rollback could not release its replacement.");
                }
                continue;
            }

            if (!TryRegister(old))
            {
                errors.Add($"{old}: The previous shortcut could not be restored and is now inactive.");
            }
        }
    }

    private bool TryRegister(HotkeyBinding binding)
    {
        if (!register(binding.Id, binding.Hotkey))
        {
            return false;
        }

        _active[binding.Id] = new(binding.Action, binding.Hotkey.Clone());
        return true;
    }

    private bool TryUnregister(HotkeyBinding binding)
    {
        if (!unregister(binding.Id))
        {
            return false;
        }

        _active.Remove(binding.Id);
        return true;
    }
}
