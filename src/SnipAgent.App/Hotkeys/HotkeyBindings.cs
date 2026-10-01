using SnipAgent.App.Models;

namespace SnipAgent.App.Hotkeys;

public sealed record HotkeyBinding(HotkeyAction Action, HotkeyDefinition Hotkey)
{
    public int Id => Action switch
    {
        HotkeyAction.ConfiguredLastUsed => 0xCA92,
        HotkeyAction.RegionAiSkills => 0xCA94,
        HotkeyAction.RegionLlm => 0xCA95,
        HotkeyAction.FullScreen => 0xCA96,
        HotkeyAction.Region => 0xCA97,
        _ => throw new ArgumentOutOfRangeException(nameof(Action))
    };

    public string Name => Action switch
    {
        HotkeyAction.ConfiguredLastUsed => "Global hotkey",
        HotkeyAction.RegionAiSkills => "AI Skills",
        HotkeyAction.RegionLlm => "Extract Text",
        HotkeyAction.FullScreen => "Full Screen",
        HotkeyAction.Region => "Region",
        _ => throw new ArgumentOutOfRangeException(nameof(Action))
    };

    public override string ToString() => $"{Name} ({Hotkey})";
}

public sealed record HotkeyFailure(HotkeyBinding Binding, string Reason)
{
    public override string ToString() => $"{Binding}: {Reason}";
}

public static class HotkeyBindings
{
    public static IReadOnlyList<HotkeyBinding> FromSettings(AppSettings settings) =>
    [
        new(HotkeyAction.RegionAiSkills, settings.DedicatedHotkeys.AiSkills.Clone()),
        new(HotkeyAction.RegionLlm, settings.DedicatedHotkeys.ExtractText.Clone()),
        new(HotkeyAction.FullScreen, settings.DedicatedHotkeys.FullScreen.Clone()),
        new(HotkeyAction.Region, settings.DedicatedHotkeys.Region.Clone()),
        new(HotkeyAction.ConfiguredLastUsed, settings.Hotkey.Clone())
    ];

    public static IReadOnlyList<HotkeyFailure> Validate(IReadOnlyList<HotkeyBinding> bindings)
    {
        var failures = new List<HotkeyFailure>();
        foreach (var binding in bindings)
        {
            if (!binding.Hotkey.IsValid())
            {
                failures.Add(new(binding, "Choose a modifier and a valid non-modifier key."));
                continue;
            }

            var other = bindings.FirstOrDefault(candidate =>
                candidate.Action != binding.Action &&
                candidate.Hotkey.HasSameCombination(binding.Hotkey));
            if (other is not null)
            {
                failures.Add(new(binding, $"Also assigned to {other.Name}. Choose different combinations."));
            }
        }

        return failures;
    }
}
