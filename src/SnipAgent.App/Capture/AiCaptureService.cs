using System.Drawing;
using System.Drawing.Imaging;
using System.ClientModel;
using System.IO;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OpenAI;
using SnipAgent.App.Models;

namespace SnipAgent.App.Capture;

/// <summary>
/// Talks to the user-configured OpenAI-compatible endpoint for the "AI capture"
/// feature: either extracting a screenshot's content as Markdown ("Use AI capture")
/// or answering/acting on the configured Prompt using the screenshot as context
/// ("Use AI to answer"), optionally with tools from configured MCP servers.
/// </summary>
public static class AiCaptureService
{
    internal const string MarkdownExtractionPromptResourceName =
        "SnipAgent.App.Assets.ai-capture-markdown-prompt.txt";

    private static readonly Lazy<string> MarkdownExtractionPrompt = new(LoadMarkdownExtractionPrompt);

    /// <summary>
    /// Runs the "Use AI capture" flow: extracts the screenshot's visible content as
    /// well-formatted Markdown. Blocks the calling thread until the response is
    /// received, matching the rest of the capture pipeline's synchronous style.
    /// </summary>
    public static string ExtractMarkdown(Bitmap bitmap, AppSettings settings)
        => Task.Run(() => ExtractMarkdownAsync(bitmap, settings)).GetAwaiter().GetResult();

    private static async Task<string> ExtractMarkdownAsync(Bitmap bitmap, AppSettings settings, CancellationToken cancellationToken = default)
    {
        var client = BuildChatClient(settings);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, MarkdownExtractionPrompt.Value),
            new(ChatRole.User, [ToImageContent(bitmap)]),
        };

        var response = await client.GetResponseAsync(messages, cancellationToken: cancellationToken).ConfigureAwait(false);
        return response.Text ?? string.Empty;
    }

    internal static string LoadMarkdownExtractionPrompt()
    {
        using var stream = typeof(AiCaptureService).Assembly
            .GetManifestResourceStream(MarkdownExtractionPromptResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded AI capture prompt '{MarkdownExtractionPromptResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        var prompt = reader.ReadToEnd();

        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new InvalidOperationException(
                $"Embedded AI capture prompt '{MarkdownExtractionPromptResourceName}' is empty.");
        }

        return prompt;
    }

    /// <summary>
    /// Runs the "Use AI to answer" flow: uses the supplied <see cref="AiSkillsTemplate"/>'s
    /// prompt as the task or question, with the screenshot as context, optionally invoking
    /// a hosted web search tool and/or tools from enabled MCP servers. Returns the model's answer text.
    /// </summary>
    public static async Task<string> AnswerAsync(
        Bitmap bitmap,
        AppSettings settings,
        AiSkillsTemplate skills,
        CancellationToken cancellationToken = default)
    {
        // The Chat Completions API only supports HostedWebSearchTool via the
        // `web_search_options` request field, which the real OpenAI API rejects as an
        // unknown parameter unless the model is one of the special "-search-preview"
        // variants — it 404s/400s for regular models (gpt-4o-mini, gpt-4o, etc.) and for
        // most other OpenAI-compatible endpoints entirely. The Responses API instead
        // exposes web search as a normal tool that works with regular chat models, so
        // route there only when the selected skills actually need it; every other skills entry keeps
        // using the broadly-compatible Chat Completions API.
        var client = skills.UseWebSearch ? BuildResponsesChatClient(settings) : BuildChatClient(settings);
        var mcpClients = new List<McpClient>();

        try
        {
            var tools = new List<AITool>();

            if (skills.UseWebSearch)
            {
                tools.Add(new HostedWebSearchTool());
            }

            foreach (var server in skills.McpServers ?? [])
            {
                if (!server.Enabled ||
                    !Uri.TryCreate(server.Url, UriKind.Absolute, out var endpoint) ||
                    (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
                {
                    // Only enabled, absolute http/https endpoints are supported; a
                    // local path or command line is deliberately not runnable here.
                    continue;
                }

                try
                {
                    var transport = new HttpClientTransport(new HttpClientTransportOptions
                    {
                        Name = server.Url,
                        Endpoint = endpoint,
                        // AutoDetect (default): try Streamable HTTP first, fall back to SSE.
                    });
                    var mcpClient = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken).ConfigureAwait(false);
                    mcpClients.Add(mcpClient);
                    tools.AddRange(await mcpClient.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    // A misbehaving/unreachable MCP server shouldn't block answering
                    // the question at all; just skip its tools.
                }
            }

            var prompt = string.IsNullOrWhiteSpace(skills.Prompt)
                ? "Answer the question or complete the task shown in the image."
                : skills.Prompt;

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, "You are a helpful assistant. Use the attached screenshot as context for the user's request."),
                new(ChatRole.User, [new TextContent(prompt), ToImageContent(bitmap)]),
            };

            var options = tools.Count > 0 ? new ChatOptions { Tools = tools } : null;
            var response = await client.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
            return response.Text ?? string.Empty;
        }
        finally
        {
            foreach (var mcpClient in mcpClients)
            {
                await mcpClient.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Resolves which configured <see cref="AiSkillsTemplate"/> to preselect: the one matching
    /// <see cref="AiCaptureSettings.SelectedAiSkillsName"/> if present, otherwise the first
    /// configured template, or null if the list is empty.
    /// </summary>
    internal static AiSkillsTemplate? ResolveSelectedAiSkills(AiCaptureSettings settings)
    {
        if (settings.AiSkills.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(settings.SelectedAiSkillsName))
        {
            var match = settings.AiSkills.FirstOrDefault(
                t => string.Equals(t.Name, settings.SelectedAiSkillsName, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return settings.AiSkills[0];
    }

    private static DataContent ToImageContent(Bitmap bitmap)
    {
        using var memoryStream = new MemoryStream();
        bitmap.Save(memoryStream, ImageFormat.Png);
        return new DataContent(memoryStream.ToArray(), "image/png");
    }

    private static IChatClient BuildChatClient(AppSettings settings)
    {
        var (openAiClient, model) = CreateOpenAiClient(settings);
        IChatClient baseClient = openAiClient.GetChatClient(model).AsIChatClient();

        // UseFunctionInvocation lets the client automatically execute AITools (e.g.
        // MCP client tools) the model asks to call and feed the results back in.
        return new ChatClientBuilder(baseClient).UseFunctionInvocation().Build();
    }

    /// <summary>
    /// Builds a chat client backed by the OpenAI Responses API instead of Chat
    /// Completions. Only used when the active <see cref="AiSkillsTemplate"/> has
    /// <see cref="AiSkillsTemplate.UseWebSearch"/> set — the Responses API exposes
    /// hosted web search as a normal, broadly-supported tool, whereas the Chat
    /// Completions API only supports it via a `web_search_options` request field
    /// that real OpenAI rejects for anything but its "-search-preview" models.
    /// </summary>
    private static IChatClient BuildResponsesChatClient(AppSettings settings)
    {
        var (openAiClient, model) = CreateOpenAiClient(settings);

#pragma warning disable OPENAI001 // The Responses API client is experimental in the OpenAI SDK.
        IChatClient baseClient = openAiClient.GetResponsesClient().AsIChatClient(model);
#pragma warning restore OPENAI001

        // UseFunctionInvocation lets the client automatically execute AITools (e.g.
        // MCP client tools) the model asks to call and feed the results back in.
        return new ChatClientBuilder(baseClient).UseFunctionInvocation().Build();
    }

    private static (OpenAIClient Client, string Model) CreateOpenAiClient(AppSettings settings)
    {
        var apiKey = CredentialManagerService.LoadApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "No AI capture API key is configured. Add one in Settings > AI capture.");
        }

        var clientOptions = new OpenAIClientOptions();
        if (!string.IsNullOrWhiteSpace(settings.AiCapture.BaseUrl))
        {
            clientOptions.Endpoint = new Uri(settings.AiCapture.BaseUrl);
        }

        var model = string.IsNullOrWhiteSpace(settings.AiCapture.Model)
            ? AiCaptureSettings.DefaultModel
            : settings.AiCapture.Model;

        return (new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions), model);
    }
}
