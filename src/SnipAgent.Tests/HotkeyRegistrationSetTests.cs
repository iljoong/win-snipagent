using System.Text.Json;
using SnipAgent.App.Hotkeys;
using SnipAgent.App.Models;
using SnipAgent.App.Settings;
using Xunit;

namespace SnipAgent.Tests;

public class HotkeyRegistrationSetTests
{
    [Fact]
    public void Startup_RegistersAndDispatchesFiveActionsIndependently()
    {
        var native = new FakeNative();
        var registrations = native.CreateSet();
        var bindings = HotkeyBindings.FromSettings(new AppSettings());

        Assert.Empty(registrations.RegisterAll(bindings));
        foreach (var binding in bindings)
        {
            Assert.Equal(binding.Action, registrations.FindBinding(binding.Id)?.Action);
        }
        Assert.Null(registrations.FindBinding(-1));
    }

    [Fact]
    public void Startup_ReportsAllUnavailableAndInvalidBindingsWithoutRewritingThem()
    {
        var native = new FakeNative();
        native.BlockedKeys.UnionWith(new[] { 0x41, 0x43 });
        var settings = new AppSettings();
        settings.DedicatedHotkeys.FullScreen.VirtualKey = 0;
        var registrations = native.CreateSet();

        var failures = registrations.RegisterAll(HotkeyBindings.FromSettings(settings));

        Assert.Equal(3, failures.Count);
        Assert.Contains(failures, failure => failure.Binding.Name == "AI Skills");
        Assert.Contains(failures, failure => failure.Binding.Name == "Extract Text");
        Assert.Contains(failures, failure => failure.Binding.Name == "Full Screen");
        Assert.Equal(2, native.Active.Count);
        Assert.Equal(0, settings.DedicatedHotkeys.FullScreen.VirtualKey);
    }

    [Fact]
    public void Startup_DuplicatesKeepDedicatedFirstAndNotifyMainConflict()
    {
        var native = new FakeNative();
        var registrations = native.CreateSet();
        var settings = new AppSettings { Hotkey = HotkeyDefinition.CtrlAlt(0x41) };

        var failure = Assert.Single(registrations.RegisterAll(HotkeyBindings.FromSettings(settings)));

        Assert.Equal(HotkeyAction.ConfiguredLastUsed, failure.Binding.Action);
        Assert.Contains("AI Skills", failure.Reason);
        Assert.Equal(4, native.Active.Count);
    }

    [Fact]
    public void UnchangedSet_IsNotReleasedOrRegisteredAgain()
    {
        var native = new FakeNative();
        var registrations = native.CreateSet();
        var settings = new AppSettings();
        registrations.RegisterAll(HotkeyBindings.FromSettings(settings));
        native.ResetCalls();
        var saved = false;

        var result = registrations.TryApply(HotkeyBindings.FromSettings(settings), () => saved = true);

        Assert.True(result.Success);
        Assert.True(saved);
        Assert.Equal(0, native.RegisterCalls);
        Assert.Equal(0, native.UnregisterCalls);
    }

    [Fact]
    public void Swap_AndFreedDefaultReuse_ApplyImmediatelyAndReleaseAllOnExit()
    {
        var native = new FakeNative();
        var registrations = native.CreateSet();
        var settings = new AppSettings();
        registrations.RegisterAll(HotkeyBindings.FromSettings(settings));
        (settings.Hotkey, settings.DedicatedHotkeys.AiSkills) =
            (settings.DedicatedHotkeys.AiSkills, settings.Hotkey);
        settings.DedicatedHotkeys.Region = HotkeyDefinition.CtrlAlt(0x42);
        var saved = false;
        var bindings = HotkeyBindings.FromSettings(settings);

        var result = registrations.TryApply(bindings, () => saved = true);

        Assert.True(result.Success);
        Assert.True(saved);
        AssertBindings(native, bindings);
        Assert.DoesNotContain(native.Active.Values, hotkey => hotkey.VirtualKey == 0x44);
        Assert.Empty(registrations.UnregisterAll());
        Assert.Empty(native.Active);
        Assert.All(bindings, binding => Assert.Null(registrations.FindBinding(binding.Id)));
    }

    [Fact]
    public void DuplicatePendingSet_DoesNotTouchActiveOrPersistedAssignments()
    {
        var native = new FakeNative();
        var registrations = native.CreateSet();
        var settings = new AppSettings();
        var previous = HotkeyBindings.FromSettings(settings);
        registrations.RegisterAll(previous);
        settings.DedicatedHotkeys.Region = settings.Hotkey.Clone();
        native.ResetCalls();
        var saved = false;

        var result = registrations.TryApply(HotkeyBindings.FromSettings(settings), () => saved = true);

        Assert.False(result.Success);
        Assert.False(saved);
        Assert.Equal(2, result.Errors.Count);
        Assert.Equal(0, native.UnregisterCalls);
        Assert.Equal(0, native.RegisterCalls);
        AssertBindings(native, previous);
    }

    [Fact]
    public void FailedRegistration_RestoresAllPreviousBindingsAndDoesNotPersist()
    {
        var native = new FakeNative();
        var registrations = native.CreateSet();
        var settings = new AppSettings();
        var previous = HotkeyBindings.FromSettings(settings);
        registrations.RegisterAll(previous);
        settings.DedicatedHotkeys.AiSkills = HotkeyDefinition.CtrlAlt(0x42);
        settings.DedicatedHotkeys.ExtractText = HotkeyDefinition.CtrlAlt(0x45);
        native.BlockedKeys.Add(0x45);
        var saved = false;

        var result = registrations.TryApply(HotkeyBindings.FromSettings(settings), () => saved = true);

        Assert.False(result.Success);
        Assert.False(saved);
        Assert.Contains(result.Errors, error => error.Contains("Extract Text (Ctrl+Alt+E)"));
        AssertBindings(native, previous);
    }

    [Theory]
    [InlineData("io")]
    [InlineData("access")]
    [InlineData("json")]
    public void FailedPersistence_RestoresPreviousBindings(string failure)
    {
        var native = new FakeNative();
        var registrations = native.CreateSet();
        var settings = new AppSettings();
        var previous = HotkeyBindings.FromSettings(settings);
        registrations.RegisterAll(previous);
        settings.DedicatedHotkeys.Region = HotkeyDefinition.CtrlAlt(0x42);

        var result = registrations.TryApply(HotkeyBindings.FromSettings(settings), () =>
            throw failure switch
            {
                "io" => new IOException("Write failed"),
                "access" => new UnauthorizedAccessException("Access denied"),
                _ => new JsonException("Serialization failed")
            });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("Settings could not be saved"));
        AssertBindings(native, previous);
    }

    [Fact]
    public void LockedSettingsFile_RetainsPersistedAssignmentsAndRestoresRegistrations()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SnipAgentHotkeyTests_" + Guid.NewGuid().ToString("N"));
        var service = new SettingsService(directory);
        try
        {
            var settings = service.Load();
            var native = new FakeNative();
            var registrations = native.CreateSet();
            var previous = HotkeyBindings.FromSettings(settings);
            registrations.RegisterAll(previous);
            settings.DedicatedHotkeys.Region = HotkeyDefinition.CtrlAlt(0x42);
            HotkeyApplyResult result;

            using (File.Open(service.SettingsFilePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                result = registrations.TryApply(HotkeyBindings.FromSettings(settings), () => service.Save(settings));
            }

            Assert.False(result.Success);
            Assert.Contains(result.Errors, error => error.Contains("Settings could not be saved"));
            AssertBindings(native, previous);
            Assert.Equal(0x44, service.Load().DedicatedHotkeys.Region.VirtualKey);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RegistrationRace_ReportsPreviousCombinationIfRestorationFails()
    {
        var native = new FakeNative();
        var registrations = native.CreateSet();
        var settings = new AppSettings();
        var previous = HotkeyBindings.FromSettings(settings);
        registrations.RegisterAll(previous);
        settings.DedicatedHotkeys.AiSkills = HotkeyDefinition.CtrlAlt(0x42);
        native.BeforeUnregister = _ =>
        {
            native.BlockedKeys.Add(0x42);
            native.BlockedKeys.Add(0x41);
        };
        var saved = false;

        var result = registrations.TryApply(HotkeyBindings.FromSettings(settings), () => saved = true);

        Assert.False(result.Success);
        Assert.False(saved);
        Assert.Contains(result.Errors, error => error.Contains("AI Skills (Ctrl+Alt+A)") && error.Contains("inactive"));
        Assert.Equal(4, native.Active.Count);
        Assert.Null(registrations.FindBinding(previous[0].Id));
    }

    [Fact]
    public void FailedRelease_RestoresPreviouslyReleasedActions()
    {
        var native = new FakeNative();
        var registrations = native.CreateSet();
        var settings = new AppSettings();
        var previous = HotkeyBindings.FromSettings(settings);
        registrations.RegisterAll(previous);
        settings.DedicatedHotkeys.AiSkills = HotkeyDefinition.CtrlAlt(0x42);
        settings.DedicatedHotkeys.ExtractText = HotkeyDefinition.CtrlAlt(0x45);
        native.FailUnregister = id => id == previous[1].Id;

        var result = registrations.TryApply(HotkeyBindings.FromSettings(settings),
            () => throw new InvalidOperationException("Must not persist"));

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("could not be released"));
        AssertBindings(native, previous);
    }

    [Fact]
    public void FailedRollbackRelease_ReportsInactivePreviousAction()
    {
        var native = new FakeNative();
        var registrations = native.CreateSet();
        var settings = new AppSettings();
        var previous = HotkeyBindings.FromSettings(settings);
        registrations.RegisterAll(previous);
        settings.DedicatedHotkeys.AiSkills = HotkeyDefinition.CtrlAlt(0x42);

        var result = registrations.TryApply(HotkeyBindings.FromSettings(settings), () =>
        {
            native.FailUnregister = id => id == previous[0].Id;
            throw new IOException("Write failed");
        });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("AI Skills (Ctrl+Alt+B)") && error.Contains("rollback"));
        Assert.Contains(result.Errors, error => error.Contains("AI Skills (Ctrl+Alt+A)") && error.Contains("inactive"));
    }

    [Fact]
    public void InactiveStartupAction_CanBeRepairedOnSave()
    {
        var native = new FakeNative();
        var registrations = native.CreateSet();
        var settings = new AppSettings();
        native.BlockedKeys.Add(0x41);
        registrations.RegisterAll(HotkeyBindings.FromSettings(settings));
        settings.DedicatedHotkeys.AiSkills = HotkeyDefinition.CtrlAlt(0x42);

        Assert.True(registrations.TryApply(HotkeyBindings.FromSettings(settings), () => { }).Success);
        AssertBindings(native, HotkeyBindings.FromSettings(settings));
    }

    private static void AssertBindings(FakeNative native, IReadOnlyList<HotkeyBinding> bindings)
    {
        Assert.Equal(bindings.Count, native.Active.Count);
        foreach (var binding in bindings)
        {
            Assert.True(native.Active[binding.Id].HasSameCombination(binding.Hotkey));
        }
    }

    private sealed class FakeNative
    {
        public Dictionary<int, HotkeyDefinition> Active { get; } = new();
        public HashSet<int> BlockedKeys { get; } = new();
        public Action<int>? BeforeUnregister { get; set; }
        public Func<int, bool>? FailUnregister { get; set; }
        public int RegisterCalls { get; private set; }
        public int UnregisterCalls { get; private set; }

        public HotkeyRegistrationSet CreateSet() => new(Register, Unregister);

        public void ResetCalls()
        {
            RegisterCalls = 0;
            UnregisterCalls = 0;
        }

        private bool Register(int id, HotkeyDefinition hotkey)
        {
            RegisterCalls++;
            if (BlockedKeys.Contains(hotkey.VirtualKey) || Active.ContainsKey(id) ||
                Active.Values.Any(active => active.HasSameCombination(hotkey)))
            {
                return false;
            }

            Active[id] = hotkey.Clone();
            return true;
        }

        private bool Unregister(int id)
        {
            UnregisterCalls++;
            BeforeUnregister?.Invoke(id);
            return FailUnregister?.Invoke(id) != true && Active.Remove(id);
        }
    }
}
