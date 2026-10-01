using SnipAgent.App.Models;
using SnipAgent.App.Hotkeys;
using Xunit;

namespace SnipAgent.Tests;

public class HotkeyDefinitionTests
{
    [Fact]
    public void DefaultBindings_HaveExpectedKeysAndDistinctIds()
    {
        var bindings = HotkeyBindings.FromSettings(new AppSettings());

        Assert.Equal(new[] { 0x41, 0x43, 0x46, 0x44, 0x53 },
            bindings.Select(binding => binding.Hotkey.VirtualKey).ToArray());
        Assert.All(bindings, binding =>
            Assert.Equal(ModifierFlags.Control | ModifierFlags.Alt, binding.Hotkey.Modifiers));
        Assert.Equal(5, bindings.Select(binding => binding.Id).Distinct().Count());
        Assert.Empty(HotkeyBindings.Validate(bindings));
    }

    [Fact]
    public void SameCombination_RequiresExactModifiers()
    {
        var hotkey = new HotkeyDefinition { Modifiers = ModifierFlags.Control, VirtualKey = 0x41 };

        Assert.False(hotkey.HasSameCombination(HotkeyDefinition.CtrlAlt(0x41)));
    }

    [Fact]
    public void BindingSnapshots_DoNotMutateSettingsOrOtherDefaults()
    {
        var settings = new AppSettings();
        var bindings = HotkeyBindings.FromSettings(settings);
        bindings[0].Hotkey.VirtualKey = 0x42;

        Assert.Equal(0x41, settings.DedicatedHotkeys.AiSkills.VirtualKey);
        Assert.Equal(0x41, new DedicatedHotkeySettings().AiSkills.VirtualKey);
    }

    [Theory]
    [InlineData(0, 0x41)]
    [InlineData(16, 0x41)]
    [InlineData(2, 0)]
    [InlineData(2, -1)]
    [InlineData(2, 0xFF)]
    [InlineData(2, 0x10)]
    [InlineData(2, 0x11)]
    [InlineData(2, 0x12)]
    [InlineData(2, 0x5B)]
    [InlineData(2, 0x5C)]
    [InlineData(2, 0xA0)]
    [InlineData(2, 0xA1)]
    [InlineData(2, 0xA2)]
    [InlineData(2, 0xA3)]
    [InlineData(2, 0xA4)]
    [InlineData(2, 0xA5)]
    public void InvalidDefinitions_AreRejected(int modifiers, int virtualKey)
    {
        Assert.False(new HotkeyDefinition { Modifiers = modifiers, VirtualKey = virtualKey }.IsValid());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(15)]
    public void SupportedModifiers_AreAccepted(int modifiers)
    {
        Assert.True(new HotkeyDefinition { Modifiers = modifiers, VirtualKey = 0x41 }.IsValid());
    }

    [Theory]
    [InlineData(0x41, "AI Skills")]
    [InlineData(0x43, "Extract Text")]
    [InlineData(0x46, "Full Screen")]
    [InlineData(0x44, "Region")]
    public void DuplicateMainAndDedicated_AssignmentsIdentifyBothActions(int virtualKey, string name)
    {
        var settings = new AppSettings { Hotkey = HotkeyDefinition.CtrlAlt(virtualKey) };
        var failures = HotkeyBindings.Validate(HotkeyBindings.FromSettings(settings));

        Assert.Equal(2, failures.Count);
        Assert.Contains(failures, failure => failure.ToString().Contains("Global hotkey"));
        Assert.Contains(failures, failure => failure.ToString().Contains(name));
    }

    [Fact]
    public void FreedDefault_CanBeUsedByMainShortcut()
    {
        var settings = new AppSettings { Hotkey = HotkeyDefinition.CtrlAlt(0x41) };
        settings.DedicatedHotkeys.AiSkills = HotkeyDefinition.CtrlAlt(0x42);

        Assert.Empty(HotkeyBindings.Validate(HotkeyBindings.FromSettings(settings)));
    }

    [Theory]
    [InlineData(HotkeyAction.RegionAiSkills, AiCaptureMode.Answer)]
    [InlineData(HotkeyAction.RegionLlm, AiCaptureMode.Capture)]
    [InlineData(HotkeyAction.FullScreen, AiCaptureMode.None)]
    [InlineData(HotkeyAction.Region, AiCaptureMode.None)]
    public void DedicatedActions_UseExpectedTemporaryAiMode(
        HotkeyAction action, AiCaptureMode expectedMode)
    {
        Assert.Equal(expectedMode, HotkeyActionRouting.GetAiModeOverride(action));
    }

    [Theory]
    [InlineData(HotkeyAction.ConfiguredLastUsed)]
    public void MainAction_DoesNotOverrideConfiguredAiMode(HotkeyAction action)
    {
        Assert.Null(HotkeyActionRouting.GetAiModeOverride(action));
    }
}
