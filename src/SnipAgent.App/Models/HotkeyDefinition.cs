namespace SnipAgent.App.Models;

/// <summary>
/// A global hotkey definition expressed in terms compatible with the Win32
/// RegisterHotKey API: a combination of MOD_* modifier flags plus a virtual-key code.
/// Stored this way (rather than as WPF Key/ModifierKeys) so it serializes simply and
/// maps directly onto the P/Invoke call without extra translation at registration time.
/// </summary>
public sealed class HotkeyDefinition
{
    /// <summary>Bitwise combination of MOD_ALT (1), MOD_CONTROL (2), MOD_SHIFT (4), MOD_WIN (8).</summary>
    public int Modifiers { get; set; }

    /// <summary>Virtual-key code (maps to System.Windows.Input.Key via KeyInterop).</summary>
    public int VirtualKey { get; set; }

    public static HotkeyDefinition Default => new()
    {
        // Ctrl + Alt + S. Deliberately not Ctrl+Shift+S, which is commonly already
        // claimed by other apps (OneDrive/OneNote screenshot, "Save As" in browsers,
        // etc.) and would make SnipAgent's hotkey registration fail on startup.
        Modifiers = ModifierFlags.Control | ModifierFlags.Alt,
        VirtualKey = 0x53 // 'S'
    };

    public static HotkeyDefinition CtrlAlt(int virtualKey) => new()
    {
        Modifiers = ModifierFlags.Control | ModifierFlags.Alt,
        VirtualKey = virtualKey
    };

    public HotkeyDefinition Clone() => new() { Modifiers = Modifiers, VirtualKey = VirtualKey };

    public bool HasSameCombination(HotkeyDefinition other) =>
        Modifiers == other.Modifiers && VirtualKey == other.VirtualKey;

    public bool IsValid() =>
        Modifiers != 0 &&
        (Modifiers & ~(ModifierFlags.Control | ModifierFlags.Alt | ModifierFlags.Shift | ModifierFlags.Win)) == 0 &&
        VirtualKey is > 0 and < 0xFF &&
        VirtualKey is not (0x10 or 0x11 or 0x12 or 0x5B or 0x5C or
            0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5) &&
        System.Windows.Input.KeyInterop.KeyFromVirtualKey(VirtualKey) != System.Windows.Input.Key.None;

    public override string ToString()
    {
        var parts = new List<string>();
        if ((Modifiers & ModifierFlags.Control) != 0) parts.Add("Ctrl");
        if ((Modifiers & ModifierFlags.Alt) != 0) parts.Add("Alt");
        if ((Modifiers & ModifierFlags.Shift) != 0) parts.Add("Shift");
        if ((Modifiers & ModifierFlags.Win) != 0) parts.Add("Win");

        var key = (System.Windows.Input.Key)System.Windows.Input.KeyInterop.KeyFromVirtualKey(VirtualKey);
        parts.Add(key.ToString());

        return string.Join("+", parts);
    }
}

/// <summary>Win32 RegisterHotKey MOD_* flag values.</summary>
public static class ModifierFlags
{
    public const int Alt = 0x0001;
    public const int Control = 0x0002;
    public const int Shift = 0x0004;
    public const int Win = 0x0008;
}
