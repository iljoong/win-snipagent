using SnipAgent.App.Capture;
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
}
