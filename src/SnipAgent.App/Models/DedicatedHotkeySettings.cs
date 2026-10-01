namespace SnipAgent.App.Models;

public sealed class DedicatedHotkeySettings
{
    public HotkeyDefinition AiSkills { get; set; } = HotkeyDefinition.CtrlAlt(0x41);
    public HotkeyDefinition ExtractText { get; set; } = HotkeyDefinition.CtrlAlt(0x43);
    public HotkeyDefinition FullScreen { get; set; } = HotkeyDefinition.CtrlAlt(0x46);
    public HotkeyDefinition Region { get; set; } = HotkeyDefinition.CtrlAlt(0x44);
}
