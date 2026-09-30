using System.Windows;
using System.Windows.Interop;
using SnipAgent.App.Capture;
using SnipAgent.App.Models;

namespace SnipAgent.App.Hotkeys;

/// <summary>
/// Registers global hotkeys using RegisterHotKey/UnregisterHotKey via a
/// hidden message-only window, and raises <see cref="HotkeyPressed"/> when it fires.
/// Deliberately avoids a low-level keyboard hook (simpler, lower AV suspicion, and
/// sufficient for modifier+key combos).
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    private const int ConfiguredHotkeyId = 0xCA92;
    private static readonly (int Id, HotkeyAction Action, HotkeyDefinition Hotkey)[] DedicatedHotkeys =
    {
        (0xCA94, HotkeyAction.RegionAiSkills, new() { Modifiers = ModifierFlags.Control | ModifierFlags.Alt, VirtualKey = HotkeyDefinition.CtrlAltA }),
        (0xCA95, HotkeyAction.RegionLlm, new() { Modifiers = ModifierFlags.Control | ModifierFlags.Alt, VirtualKey = HotkeyDefinition.CtrlAltC }),
        (0xCA96, HotkeyAction.FullScreen, new() { Modifiers = ModifierFlags.Control | ModifierFlags.Alt, VirtualKey = HotkeyDefinition.CtrlAltF }),
        (0xCA97, HotkeyAction.Region, new() { Modifiers = ModifierFlags.Control | ModifierFlags.Alt, VirtualKey = HotkeyDefinition.CtrlAltD }),
    };

    private readonly Window _messageWindow;
    private readonly HwndSource _hwndSource;
    private readonly Dictionary<int, HotkeyAction> _registeredHotkeys = new();

    public event EventHandler<HotkeyActionEventArgs>? HotkeyPressed;

    public HotkeyManager()
    {
        // A zero-size, never-shown window whose sole purpose is to host an HWND that
        // can receive WM_HOTKEY messages.
        _messageWindow = new Window
        {
            Width = 0,
            Height = 0,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            Visibility = Visibility.Hidden
        };
        _messageWindow.Show();
        _messageWindow.Hide();

        _hwndSource = (HwndSource)PresentationSource.FromVisual(_messageWindow)!;
        _hwndSource.AddHook(WndProc);
    }

    /// <summary>
    /// Registers the fixed shortcuts and the configured repeat-last shortcut
    /// independently. Returns the dedicated combinations that could not be registered.
    /// </summary>
    public IReadOnlyList<(HotkeyAction Action, HotkeyDefinition Hotkey)> RegisterAll(HotkeyDefinition hotkey)
    {
        var failures = new List<(HotkeyAction, HotkeyDefinition)>();
        foreach (var (id, action, dedicatedHotkey) in DedicatedHotkeys)
        {
            if (TryRegisterCore(id, action, dedicatedHotkey))
            {
                continue;
            }

            failures.Add((action, dedicatedHotkey));
        }

        if (hotkey.IsReservedDedicatedHotkey() ||
            !TryRegisterCore(ConfiguredHotkeyId, HotkeyAction.ConfiguredLastUsed, hotkey))
        {
            failures.Add((HotkeyAction.ConfiguredLastUsed, hotkey));
        }

        return failures;
    }

    public bool TryRegister(HotkeyDefinition hotkey)
    {
        UnregisterConfigured();
        return !hotkey.IsReservedDedicatedHotkey() &&
            TryRegisterCore(ConfiguredHotkeyId, HotkeyAction.ConfiguredLastUsed, hotkey);
    }

    /// <summary>
    /// Validates that a hotkey combination can be registered right now, without
    /// leaving it registered. Used by the Settings UI to test a candidate hotkey
    /// before saving, so we never silently keep an old, working hotkey registered
    /// while telling the user their new one was accepted.
    /// </summary>
    public static bool CanRegister(HwndSource probeWindowSource, HotkeyDefinition hotkey)
    {
        const int probeId = 0xCA93;
        if (hotkey.IsReservedDedicatedHotkey())
        {
            return false;
        }
        var ok = NativeMethods.RegisterHotKey(probeWindowSource.Handle, probeId, hotkey.Modifiers, hotkey.VirtualKey);
        if (ok)
        {
            NativeMethods.UnregisterHotKey(probeWindowSource.Handle, probeId);
        }
        return ok;
    }

    public void Unregister()
    {
        foreach (var id in _registeredHotkeys.Keys.ToArray())
        {
            NativeMethods.UnregisterHotKey(_hwndSource.Handle, id);
        }
        _registeredHotkeys.Clear();
    }

    private void UnregisterConfigured()
    {
        if (_registeredHotkeys.Remove(ConfiguredHotkeyId))
        {
            NativeMethods.UnregisterHotKey(_hwndSource.Handle, ConfiguredHotkeyId);
        }
    }

    private bool TryRegisterCore(int id, HotkeyAction action, HotkeyDefinition hotkey)
    {
        if (_registeredHotkeys.ContainsKey(id))
        {
            NativeMethods.UnregisterHotKey(_hwndSource.Handle, id);
            _registeredHotkeys.Remove(id);
        }

        if (!NativeMethods.RegisterHotKey(_hwndSource.Handle, id, hotkey.Modifiers, hotkey.VirtualKey))
        {
            return false;
        }

        _registeredHotkeys[id] = action;
        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY &&
            _registeredHotkeys.TryGetValue(wParam.ToInt32(), out var action))
        {
            HotkeyPressed?.Invoke(this, new HotkeyActionEventArgs(action));
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        _hwndSource.RemoveHook(WndProc);
        _hwndSource.Dispose();
        _messageWindow.Close();
    }

}

public enum HotkeyAction
{
    ConfiguredLastUsed,
    RegionAiSkills,
    RegionLlm,
    FullScreen,
    Region
}

public sealed class HotkeyActionEventArgs(HotkeyAction action) : EventArgs
{
    public HotkeyAction Action { get; } = action;
}
