using System.Windows;
using System.Windows.Controls;
using SnipAgent.App.Models;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using TextBox = System.Windows.Controls.TextBox;

namespace SnipAgent.App.Settings;

/// <summary>
/// Modal dialog for adding or editing one <see cref="AiSkillsTemplate"/> used by "Use
/// AI to answer" mode: skills name, prompt, optional hosted web search tool, and
/// optional MCP servers.
/// </summary>
public partial class AiSkillsDialog : Window
{
    private readonly IReadOnlyCollection<string> _otherSkillsNames;

    /// <summary>Live UI rows for configured MCP servers, kept in sync with <see cref="McpServersPanel"/>.</summary>
    private readonly List<(CheckBox Enabled, TextBox Url)> _mcpRows = new();

    /// <summary>The edited/created template, populated when the dialog closes with <c>DialogResult == true</c>.</summary>
    public AiSkillsTemplate Result { get; private set; } = new();

    /// <param name="template">Existing template to edit, or null to add a new one.</param>
    /// <param name="otherSkillsNames">
    /// Names of the other templates already configured (excluding <paramref name="template"/>
    /// itself), used to reject duplicate names.
    /// </param>
    public AiSkillsDialog(AiSkillsTemplate? template, IReadOnlyCollection<string> otherSkillsNames)
    {
        InitializeComponent();

        _otherSkillsNames = otherSkillsNames;
        Title = template is null ? "Add AI Skills" : "Edit AI Skills";

        SkillsNameTextBox.Text = template?.Name ?? string.Empty;
        SkillsPromptTextBox.Text = template?.Prompt ?? string.Empty;
        UseWebSearchCheckBox.IsChecked = template?.UseWebSearch ?? false;

        foreach (var server in template?.McpServers ?? new List<McpServerEntry>())
        {
            AddMcpRow(server.Enabled, server.Url);
        }
    }

    private void OnMcpAddClick(object sender, RoutedEventArgs e) => AddMcpRow(enabled: true, url: string.Empty);

    /// <summary>Adds one [checkbox] [url text field] [remove] row to the MCP servers list.</summary>
    private void AddMcpRow(bool enabled, string url)
    {
        var row = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };

        var removeButton = new Button
        {
            Content = "\u2715",
            Width = 24,
            Margin = new Thickness(6, 0, 0, 0),
            ToolTip = "Remove this MCP server"
        };
        DockPanel.SetDock(removeButton, Dock.Right);

        var enabledCheckBox = new CheckBox
        {
            IsChecked = enabled,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        };
        DockPanel.SetDock(enabledCheckBox, Dock.Left);

        var urlTextBox = new TextBox
        {
            Text = url,
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "HTTP(S) endpoint URL of the remote MCP server, " +
                      "e.g. https://example.com/mcp"
        };

        row.Children.Add(enabledCheckBox);
        row.Children.Add(removeButton);
        row.Children.Add(urlTextBox);

        removeButton.Click += (_, _) =>
        {
            McpServersPanel.Children.Remove(row);
            _mcpRows.RemoveAll(r => ReferenceEquals(r.Url, urlTextBox));
        };

        McpServersPanel.Children.Add(row);
        _mcpRows.Add((enabledCheckBox, urlTextBox));
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        var name = SkillsNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ValidationText.Text = "Skills name is required.";
            return;
        }

        if (_otherSkillsNames.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            ValidationText.Text = "An AI skills entry with this name already exists.";
            return;
        }

        Result = new AiSkillsTemplate
        {
            Name = name,
            Prompt = SkillsPromptTextBox.Text,
            UseWebSearch = UseWebSearchCheckBox.IsChecked == true,
            McpServers = _mcpRows
                .Where(row => !string.IsNullOrWhiteSpace(row.Url.Text))
                .Select(row => new McpServerEntry { Enabled = row.Enabled.IsChecked == true, Url = row.Url.Text.Trim() })
                .ToList()
        };

        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
