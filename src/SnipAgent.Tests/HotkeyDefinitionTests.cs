using SnipAgent.App.Models;
using Xunit;

namespace SnipAgent.Tests;

public class HotkeyDefinitionTests
{
    [Theory]
    [InlineData(HotkeyDefinition.CtrlAltA)]
    [InlineData(HotkeyDefinition.CtrlAltC)]
    [InlineData(HotkeyDefinition.CtrlAltF)]
    [InlineData(HotkeyDefinition.CtrlAltD)]
    public void DedicatedCombinations_AreReserved(int virtualKey)
    {
        var hotkey = new HotkeyDefinition
        {
            Modifiers = ModifierFlags.Control | ModifierFlags.Alt,
            VirtualKey = virtualKey
        };

        Assert.True(hotkey.IsReservedDedicatedHotkey());
    }

    [Fact]
    public void ReservedCheck_RequiresExactModifiers()
    {
        var hotkey = new HotkeyDefinition
        {
            Modifiers = ModifierFlags.Control,
            VirtualKey = HotkeyDefinition.CtrlAltA
        };

        Assert.False(hotkey.IsReservedDedicatedHotkey());
    }

    [Fact]
    public void DedicatedDefinitions_AreDistinct()
    {
        var combinations = HotkeyDefinition.ReservedDedicatedHotkeys
            .Select(hotkey => (hotkey.Modifiers, hotkey.VirtualKey))
            .ToList();

        Assert.Equal(4, combinations.Count);
        Assert.Equal(4, combinations.Distinct().Count());
    }
}
