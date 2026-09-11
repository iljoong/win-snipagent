using SnipAgent.App.Capture;
using SnipAgent.App.Models;
using Xunit;

namespace SnipAgent.Tests;

public class AiCaptureServiceTests
{
    [Fact]
    public void LoadMarkdownExtractionPrompt_LoadsDetailedEmbeddedPrompt()
    {
        var prompt = AiCaptureService.LoadMarkdownExtractionPrompt();

        Assert.Contains("natural reading order", prompt);
        Assert.Contains("Markdown tables", prompt);
        Assert.Contains("fenced code blocks", prompt);
        Assert.Contains("[illegible]", prompt);
        Assert.Contains("Return only the reconstructed Markdown", prompt);
    }

    [Fact]
    public void ResolveSelectedAiSkills_ReturnsRememberedSkillIgnoringCase()
    {
        var settings = new AiCaptureSettings
        {
            SelectedAiSkillsName = "second",
            AiSkills =
            [
                new AiSkillsTemplate { Name = "First" },
                new AiSkillsTemplate { Name = "Second" },
            ]
        };

        var selected = AiCaptureService.ResolveSelectedAiSkills(settings);

        Assert.Same(settings.AiSkills[1], selected);
    }

    [Fact]
    public void ResolveSelectedAiSkills_WhenRememberedSkillIsMissing_ReturnsFirstSkill()
    {
        var settings = new AiCaptureSettings
        {
            SelectedAiSkillsName = "Deleted",
            AiSkills =
            [
                new AiSkillsTemplate { Name = "First" },
                new AiSkillsTemplate { Name = "Second" },
            ]
        };

        var selected = AiCaptureService.ResolveSelectedAiSkills(settings);

        Assert.Same(settings.AiSkills[0], selected);
    }

    [Fact]
    public void ResolveSelectedAiSkills_WhenNoSkillsExist_ReturnsNull()
    {
        var settings = new AiCaptureSettings { AiSkills = [] };

        var selected = AiCaptureService.ResolveSelectedAiSkills(settings);

        Assert.Null(selected);
    }
}
