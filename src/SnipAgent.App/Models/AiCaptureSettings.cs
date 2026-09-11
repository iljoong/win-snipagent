namespace SnipAgent.App.Models;

/// <summary>
/// Settings for the "AI capture" feature group. The API key is deliberately not a
/// property here — it's stored/retrieved separately via Windows Credential Manager
/// (see <see cref="Capture.CredentialManagerService"/>) so it never round-trips
/// through the plaintext settings.json file.
/// </summary>
public sealed class AiCaptureSettings
{
    public AiCaptureMode Mode { get; set; } = AiCaptureMode.WindowsOcr;

    /// <summary>Base URL of the OpenAI-compatible endpoint (e.g. https://api.openai.com/v1).</summary>
    public string BaseUrl { get; set; } = DefaultBaseUrl;

    /// <summary>Model/deployment name to request, e.g. gpt-5.6-sol.</summary>
    public string Model { get; set; } = DefaultModel;

    /// <summary>
    /// Named prompt templates the user can pick between for "Use AI to answer" mode
    /// (e.g. "Translate to Korean", "Describe"). Ignored by "Use AI capture" mode
    /// (which always extracts Markdown).
    /// </summary>
    public List<AiSkillsTemplate> AiSkills { get; set; } = CreateDefaultAiSkills();

    /// <summary>Name of the <see cref="AiSkillsTemplate"/> last selected for "Use AI to answer" mode.</summary>
    public string? SelectedAiSkillsName { get; set; }

    public const string DefaultBaseUrl = "https://api.openai.com/v1";
    public const string DefaultModel = "gpt-5.6-sol";

    /// <summary>The built-in AI Skills templates offered out of the box on a fresh install.</summary>
    public static List<AiSkillsTemplate> CreateDefaultAiSkills() => new()
    {
        new AiSkillsTemplate
        {
            Name = "Translate to Korean",
            Prompt = "Translate all readable text in the screenshot into natural Korean.\n" +
                     "Preserve the original meaning, tone, structure, proper nouns, code, and URLs.\n" +
                     "Return only the translation. Mark unreadable text as [illegible] instead of guessing.",
        },
        new AiSkillsTemplate
        {
            Name = "Describe Screenshot",
            Prompt = "Describe the screenshot accurately and concisely.\n" +
                     "Identify the main content, relevant visible text, layout, and UI state.\n" +
                     "Separate direct observations from inferences, and do not invent unreadable or hidden details.",
        },
        new AiSkillsTemplate
        {
            Name = "Research & Explain",
            Prompt = "Analyze the content in the screenshot and identify its key topics, claims, and trends.\n" +
                     "Explain the relevant background, significance, and practical implications.\n" +
                     "Use web search to verify time-sensitive claims and add current context. Prefer authoritative primary sources.\n" +
                     "Cite web-supported claims with inline links and finish with a brief Sources list.\n" +
                     "Clearly label uncertainty or inference, and do not guess at unreadable content.",
            UseWebSearch = true,
        },
        new AiSkillsTemplate
        {
            Name = "Azure Expert",
            Prompt = "Answer the question or complete the task shown in the screenshot accurately and concisely.\n" +
                     "Use the Microsoft Learn documentation tools to verify claims about Microsoft or Azure products and prefer official documentation.\n" +
                     "Cite supporting sources with inline links and finish with a brief Sources list.\n" +
                     "State any necessary assumptions, and do not guess at unreadable or missing details.",
            McpServers = new List<McpServerEntry> { new() { Enabled = true, Url = "https://learn.microsoft.com/api/mcp" } },
        },
    };
}
