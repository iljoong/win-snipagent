using System.Text.Json;
using SnipAgent.App.Models;
using SnipAgent.App.Settings;
using Xunit;

namespace SnipAgent.Tests;

/// <summary>
/// Tests SettingsService's JSON persistence and, importantly, its recovery-to-defaults
/// behavior when the settings file is missing or corrupted — a tray-only background
/// app must never fail to start because of a bad settings file.
/// </summary>
public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir;

    public SettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SnipAgentTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private SettingsService CreateServiceWithFile(string? existingJson = null)
    {
        var service = new SettingsService(_tempDir);
        if (existingJson is not null)
        {
            File.WriteAllText(service.SettingsFilePath, existingJson);
        }
        return service;
    }

    [Fact]
    public void Load_WhenFileMissing_CreatesDefaultsAndPersistsThem()
    {
        var service = CreateServiceWithFile();

        var settings = service.Load();

        Assert.Equal(CaptureMode.Region, settings.LastCaptureMode);
        Assert.True(File.Exists(service.SettingsFilePath));
    }

    [Fact]
    public void Load_WhenFileCorrupted_RecoversWithDefaults()
    {
        var service = CreateServiceWithFile("{ not valid json ][");

        var settings = service.Load();

        Assert.NotNull(settings);
        Assert.False(string.IsNullOrWhiteSpace(settings.SaveFolder));
    }

    [Fact]
    public void SaveThenLoad_RoundTripsValues()
    {
        var service = CreateServiceWithFile();
        var settings = service.Load();
        settings.LastCaptureMode = CaptureMode.FullScreen;
        settings.FilenamePattern = "Custom_{date}";
        settings.SaveFolder = _tempDir;
        settings.AiCapture.SelectedAiSkillsName = "AI Tutor";
        settings.AiAnswerOverlayWidth = 880;
        settings.AiAnswerOverlayHeight = 640;

        service.Save(settings);
        var reloaded = service.Load();

        Assert.Equal(CaptureMode.FullScreen, reloaded.LastCaptureMode);
        Assert.Equal("Custom_{date}", reloaded.FilenamePattern);
        Assert.Equal(_tempDir, reloaded.SaveFolder);
        Assert.Equal("AI Tutor", reloaded.AiCapture.SelectedAiSkillsName);
        Assert.Equal(880, reloaded.AiAnswerOverlayWidth);
        Assert.Equal(640, reloaded.AiAnswerOverlayHeight);
    }

    [Fact]
    public void Load_WhenSaveFolderIsBlank_FallsBackToDefault()
    {
        var json = JsonSerializer.Serialize(new { SaveFolder = "", FilenamePattern = "x", LastCaptureMode = 0, Hotkey = new { Modifiers = 2, VirtualKey = 0x53 } });
        var service = CreateServiceWithFile(json);

        var settings = service.Load();

        Assert.False(string.IsNullOrWhiteSpace(settings.SaveFolder));
    }

    [Fact]
    public void Load_WhenCaptureDelayUnsupported_NormalizesToImmediate()
    {
        var json = JsonSerializer.Serialize(new
        {
            SaveFolder = _tempDir,
            FilenamePattern = "x",
            LastCaptureMode = 0,
            CaptureDelaySeconds = 7,
            Hotkey = new { Modifiers = 2, VirtualKey = 0x53 }
        });
        var service = CreateServiceWithFile(json);

        var settings = service.Load();

        Assert.Equal(0, settings.CaptureDelaySeconds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void SaveThenLoad_PreservesSupportedCaptureDelay(int delay)
    {
        var service = CreateServiceWithFile();
        var settings = service.Load();
        settings.SaveFolder = _tempDir;
        settings.CaptureDelaySeconds = delay;

        service.Save(settings);
        var reloaded = service.Load();

        Assert.Equal(delay, reloaded.CaptureDelaySeconds);
    }

    [Fact]
    public void NewSettings_DefaultCaptureDelayIsImmediate()
    {
        Assert.Equal(0, new AppSettings().CaptureDelaySeconds);
    }

    [Fact]
    public void NewSettings_SavingDefaultsToSaveToFile()
    {
        Assert.Equal(SavingOption.SaveToFile, new AppSettings().Saving);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(0)]
    public void NormalizeOverlaySize_WhenInvalid_ReturnsDefaults(double value)
    {
        Assert.Equal(
            AppSettings.DefaultAiAnswerOverlayWidth,
            AppSettings.NormalizeAiAnswerOverlayWidth(value));
        Assert.Equal(
            AppSettings.DefaultAiAnswerOverlayHeight,
            AppSettings.NormalizeAiAnswerOverlayHeight(value));
    }

    [Fact]
    public void Load_WhenOverlaySizeUndersized_NormalizesToDefaults()
    {
        var json = JsonSerializer.Serialize(new
        {
            SaveFolder = _tempDir,
            FilenamePattern = "x",
            LastCaptureMode = 0,
            Hotkey = new { Modifiers = 2, VirtualKey = 0x53 },
            AiAnswerOverlayWidth = AppSettings.MinAiAnswerOverlayWidth - 1,
            AiAnswerOverlayHeight = AppSettings.MinAiAnswerOverlayHeight - 1
        });
        var service = CreateServiceWithFile(json);

        var settings = service.Load();

        Assert.Equal(AppSettings.DefaultAiAnswerOverlayWidth, settings.AiAnswerOverlayWidth);
        Assert.Equal(AppSettings.DefaultAiAnswerOverlayHeight, settings.AiAnswerOverlayHeight);
    }

    [Fact]
    public void NewSettings_OverlaySizeUsesDefaults()
    {
        var settings = new AppSettings();

        Assert.Equal(AppSettings.DefaultAiAnswerOverlayWidth, settings.AiAnswerOverlayWidth);
        Assert.Equal(AppSettings.DefaultAiAnswerOverlayHeight, settings.AiAnswerOverlayHeight);
    }

    [Fact]
    public void NewAiCaptureSettings_UseOptimizedModelAndSkillsDefaults()
    {
        var settings = new AiCaptureSettings();

        Assert.Equal("gpt-5.6-sol", settings.Model);
        Assert.Equal(
            new[] { "Translate to Korean", "Describe Screenshot", "AI Tutor", "Research & Explain", "Azure Expert" },
            settings.AiSkills.Select(skills => skills.Name));

        // Looked up by name rather than index so reordering the built-ins doesn't
        // silently move these capability assertions onto the wrong skill.
        var researchSkills = settings.AiSkills.Single(s => s.Name == "Research & Explain");
        Assert.True(researchSkills.UseWebSearch);
        Assert.Contains("authoritative primary sources", researchSkills.Prompt);

        var azureSkills = settings.AiSkills.Single(s => s.Name == "Azure Expert");
        Assert.Single(azureSkills.McpServers);
        Assert.Equal("https://learn.microsoft.com/api/mcp", azureSkills.McpServers[0].Url);

        var tutorSkills = settings.AiSkills.Single(s => s.Name == "AI Tutor");
        Assert.False(tutorSkills.UseWebSearch);
        Assert.Empty(tutorSkills.McpServers);
        Assert.Contains("step by step", tutorSkills.Prompt);
    }

    [Fact]
    public void CreateDefaultAiSkills_ReturnsIndependentInstancesPerCall()
    {
        // The defaults are deserialized from a cached copy of the embedded JSON asset;
        // callers mutate what they get back, so that must never leak between calls.
        // This is what lets "Restore defaults" hand its result straight to the working
        // list without corrupting the defaults for the next reset.
        var first = AiCaptureSettings.CreateDefaultAiSkills();
        var firstCount = first.Count;
        first[0].Prompt = "mutated";
        first.Single(s => s.Name == "Azure Expert").McpServers.Clear();
        first.RemoveAt(1);

        var second = AiCaptureSettings.CreateDefaultAiSkills();

        Assert.Equal(firstCount, second.Count);
        Assert.NotSame(first[0], second[0]);
        Assert.DoesNotContain("mutated", second[0].Prompt);
        Assert.Single(second.Single(s => s.Name == "Azure Expert").McpServers);
    }

    [Theory]
    [InlineData(SavingOption.SaveToFile)]
    [InlineData(SavingOption.SaveToClipboard)]
    [InlineData(SavingOption.SaveToFileAndClipboard)]
    [InlineData(SavingOption.Off)]
    public void SaveThenLoad_PreservesSavingOption(SavingOption saving)
    {
        var service = CreateServiceWithFile();
        var settings = service.Load();
        settings.SaveFolder = _tempDir;
        settings.Saving = saving;

        service.Save(settings);
        var reloaded = service.Load();

        Assert.Equal(saving, reloaded.Saving);
    }
}
