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
    private readonly Window _messageWindow;
    private readonly HwndSource _hwndSource;
    private readonly HotkeyRegistrationSet _registrations;

    public event EventHandler<HotkeyActionEventArgs>? HotkeyPressed;
    public event EventHandler<HotkeyActionEventArgs>? PreviewHotkeyPressed;

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
        _registrations = new HotkeyRegistrationSet(
            (id, hotkey) => NativeMethods.RegisterHotKey(_hwndSource.Handle, id, hotkey.Modifiers, hotkey.VirtualKey),
            id => NativeMethods.UnregisterHotKey(_hwndSource.Handle, id));
    }

    public IReadOnlyList<HotkeyFailure> RegisterAll(AppSettings settings) =>
        _registrations.RegisterAll(HotkeyBindings.FromSettings(settings));

    public HotkeyApplyResult TryApply(AppSettings settings, Action persist) =>
        _registrations.TryApply(HotkeyBindings.FromSettings(settings), persist);

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY &&
            _registrations.FindBinding(wParam.ToInt32()) is { } binding)
        {
            var args = new HotkeyActionEventArgs(binding.Action, binding.Hotkey.Clone());
            PreviewHotkeyPressed?.Invoke(this, args);
            if (!args.Handled)
            {
                HotkeyPressed?.Invoke(this, args);
            }
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var error in _registrations.UnregisterAll())
        {
            System.Diagnostics.Trace.TraceError(error);
        }
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

public sealed class HotkeyActionEventArgs(HotkeyAction action, HotkeyDefinition hotkey) : EventArgs
{
    public HotkeyAction Action { get; } = action;
    public HotkeyDefinition Hotkey { get; } = hotkey;
    public bool Handled { get; set; }
}

public static class HotkeyActionRouting
{
    public static AiCaptureMode? GetAiModeOverride(HotkeyAction action) =>
        action switch
        {
            HotkeyAction.RegionAiSkills => AiCaptureMode.Answer,
            HotkeyAction.RegionLlm => AiCaptureMode.Capture,
            HotkeyAction.FullScreen => AiCaptureMode.None,
            HotkeyAction.Region => AiCaptureMode.None,
            _ => null
        };
}
