using System.IO;
using System.Text.Json;

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

    /// <summary>
    /// Logical name of the embedded JSON asset (source: <c>Assets\default-ai-skills.json</c>)
    /// holding the built-in AI Skills templates. Pinned via <c>LogicalName</c> in the csproj
    /// so renaming the folder or root namespace can't silently break this lookup.
    /// </summary>
    internal const string DefaultAiSkillsResourceName = "SnipAgent.App.Assets.default-ai-skills.json";

    private static readonly JsonSerializerOptions DefaultAiSkillsJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// The asset is immutable for the process lifetime, so read it off the assembly once.
    /// Only the raw JSON is cached — each caller gets freshly deserialized instances
    /// (see <see cref="CreateDefaultAiSkills"/>).
    /// </summary>
    private static readonly Lazy<string?> DefaultAiSkillsJson = new(ReadDefaultAiSkillsJson);

    /// <summary>
    /// The built-in AI Skills templates offered out of the box on a fresh install, loaded
    /// from the embedded <c>Assets\default-ai-skills.json</c> asset.
    /// <para>
    /// Returns a new list of new instances on every call: callers (the settings dialog's
    /// "Restore defaults", and the <see cref="AiSkills"/> initializer) mutate what they get
    /// back, which must not leak into the cached defaults.
    /// </para>
    /// <para>
    /// Never throws — this runs while constructing settings for a tray-only background app
    /// that must always be able to start, so a missing or malformed asset degrades to an
    /// empty list rather than taking the app down.
    /// </para>
    /// </summary>
    public static List<AiSkillsTemplate> CreateDefaultAiSkills()
    {
        var json = DefaultAiSkillsJson.Value;
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<AiSkillsTemplate>();
        }

        List<AiSkillsTemplate>? templates;
        try
        {
            templates = JsonSerializer.Deserialize<List<AiSkillsTemplate>>(json, DefaultAiSkillsJsonOptions);
        }
        catch (JsonException)
        {
            return new List<AiSkillsTemplate>();
        }

        if (templates is null)
        {
            return new List<AiSkillsTemplate>();
        }

        // Drop unusable entries and normalize nullable collections the JSON may omit.
        var usable = new List<AiSkillsTemplate>(templates.Count);
        foreach (var template in templates)
        {
            if (template is null ||
                string.IsNullOrWhiteSpace(template.Name) ||
                string.IsNullOrWhiteSpace(template.Prompt))
            {
                continue;
            }

            template.McpServers ??= new List<McpServerEntry>();
            usable.Add(template);
        }

        return usable;
    }

    private static string? ReadDefaultAiSkillsJson()
    {
        try
        {
            using var stream = typeof(AiCaptureSettings).Assembly
                .GetManifestResourceStream(DefaultAiSkillsResourceName);
            if (stream is null)
            {
                return null;
            }

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (Exception ex) when (ex is IOException or FileLoadException or NotSupportedException)
        {
            return null;
        }
    }
}

