using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SnipAgent.App.Capture;
using SnipAgent.App.Hotkeys;
using SnipAgent.App.Models;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using WinFormsFolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;

namespace SnipAgent.App.Settings;

/// <summary>
/// Settings window: save folder, filename pattern (with live preview), global
/// hotkey remapping, and AI capture configuration. Shortcut changes are validated
/// and registered as a complete set on Save.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly HotkeyManager _hotkeyManager;
    private AppSettings _workingCopy;
    private readonly Dictionary<HotkeyAction, HotkeyDefinition> _pendingHotkeys;
    private readonly Dictionary<HotkeyAction, (TextBox Field, TextBlock Validation)> _hotkeyFields;

    /// <summary>
    /// Working list of AI Skills templates, kept in sync with <see cref="AiSkillsComboBox"/>.
    /// Cloned from settings on load so Cancel discards any add/edit/delete.
    /// </summary>
    private List<AiSkillsTemplate> _aiSkills = new();

    public SettingsWindow(SettingsService settingsService, HotkeyManager hotkeyManager)
    {
        InitializeComponent();

        _settingsService = settingsService;
        _hotkeyManager = hotkeyManager;
        _workingCopy = _settingsService.Load();
        _pendingHotkeys = HotkeyBindings.FromSettings(_workingCopy)
            .ToDictionary(binding => binding.Action, binding => binding.Hotkey);
        _hotkeyFields = new()
        {
            [HotkeyAction.ConfiguredLastUsed] = (HotkeyTextBox, HotkeyValidationText),
            [HotkeyAction.RegionAiSkills] = (AiSkillsHotkeyTextBox, AiSkillsHotkeyValidationText),
            [HotkeyAction.RegionLlm] = (ExtractTextHotkeyTextBox, ExtractTextHotkeyValidationText),
            [HotkeyAction.FullScreen] = (FullScreenHotkeyTextBox, FullScreenHotkeyValidationText),
            [HotkeyAction.Region] = (RegionHotkeyTextBox, RegionHotkeyValidationText)
        };
        foreach (var (action, controls) in _hotkeyFields)
        {
            controls.Field.Text = _pendingHotkeys[action].ToString();
        }
        _hotkeyManager.PreviewHotkeyPressed += OnRegisteredHotkeyPressed;
        Closed += (_, _) => _hotkeyManager.PreviewHotkeyPressed -= OnRegisteredHotkeyPressed;

        SaveFolderTextBox.Text = _workingCopy.SaveFolder;
        FilenamePatternTextBox.Text = _workingCopy.FilenamePattern;
        PopulateCaptureDelayChoices();
        PopulateSavingOptionChoices();
        PopulateAiCaptureSection();
        UpdateFilenamePreview();
    }


    /// <summary>
    /// Surfaces the capture delay as a fixed set of choices (Off, 3s, 5s, 10s) rather
    /// than free-form input, so only supported values can ever be selected/saved.
    /// </summary>
    private void PopulateCaptureDelayChoices()
    {
        CaptureDelayComboBox.ItemsSource = AppSettings.SupportedCaptureDelays
            .Select(seconds => new DelayChoice(
                seconds == 0 ? "Off" : $"{seconds} seconds", seconds))
            .ToList();
        CaptureDelayComboBox.DisplayMemberPath = nameof(DelayChoice.Label);
        CaptureDelayComboBox.SelectedValuePath = nameof(DelayChoice.Seconds);
        CaptureDelayComboBox.SelectedValue = AppSettings.NormalizeCaptureDelay(_workingCopy.CaptureDelaySeconds);
    }

    private sealed record DelayChoice(string Label, int Seconds);

    /// <summary>
    /// Surfaces the saving option as a fixed set of choices (save to file, clipboard,
    /// both, or off), replacing the earlier "Save to clipboard" checkbox.
    /// </summary>
    private void PopulateSavingOptionChoices()
    {
        SavingOptionComboBox.ItemsSource = new List<SavingChoice>
        {
            new("Save to file", SavingOption.SaveToFile),
            new("Save to clipboard", SavingOption.SaveToClipboard),
            new("Save to file and clipboard", SavingOption.SaveToFileAndClipboard),
            new("Off", SavingOption.Off),
        };
        SavingOptionComboBox.DisplayMemberPath = nameof(SavingChoice.Label);
        SavingOptionComboBox.SelectedValuePath = nameof(SavingChoice.Option);
        SavingOptionComboBox.SelectedValue = _workingCopy.Saving;
    }

    private sealed record SavingChoice(string Label, SavingOption Option);

    private sealed record AiModeChoice(string Label, AiCaptureMode Mode, bool IsEnabled);

    private void OnExtractMethodSelectionChanged(object sender, SelectionChangedEventArgs e)
        => UpdateAiSkillsEnabled();

    private void UpdateAiSkillsEnabled()
    {
        if (AiSkillsDockPanel is not null)
        {
            AiSkillsDockPanel.IsEnabled =
                ExtractMethodComboBox.SelectedItem is AiModeChoice
                {
                    IsEnabled: true,
                    Mode: AiCaptureMode.Answer
                };
        }
    }

    /// <summary>
    /// Populates the AI capture mode dropdown, text fields, and MCP server rows from
    /// the working copy. The API key itself is never loaded into the UI (it lives in
    /// Windows Credential Manager) — only a hint about whether one is already saved.
    /// </summary>
    private void PopulateAiCaptureSection()
    {
        var choices = new List<AiModeChoice>
        {
            new("None", AiCaptureMode.None, false),
            new("Extract text (Windows OCR)", AiCaptureMode.WindowsOcr, true),
            new("Extract text (Using LLM)", AiCaptureMode.Capture, true),
            new("Use AI Skills", AiCaptureMode.Answer, true),
        };
        ExtractMethodComboBox.ItemsSource = choices;
        ExtractMethodComboBox.DisplayMemberPath = nameof(AiModeChoice.Label);
        ExtractMethodComboBox.SelectedItem = choices.FirstOrDefault(choice =>
            choice.Mode == _workingCopy.AiCapture.Mode) ?? choices[0];
        UpdateAiSkillsEnabled();

        AiBaseUrlTextBox.Text = _workingCopy.AiCapture.BaseUrl;
        AiModelTextBox.Text = _workingCopy.AiCapture.Model;

        AiApiKeyHintText.Text = CredentialManagerService.HasApiKey()
            ? "An API key is already saved. Leave blank to keep it, or enter a new value to replace it."
            : "No API key saved yet.";

        // Deep-clone so Cancel doesn't leave partial add/edit/delete changes behind in
        // the loaded settings object.
        _aiSkills = _workingCopy.AiCapture.AiSkills.Select(CloneAiSkills).ToList();
        var selectedSkills = _aiSkills.FirstOrDefault(t =>
            string.Equals(t.Name, _workingCopy.AiCapture.SelectedAiSkillsName, StringComparison.OrdinalIgnoreCase));
        RefreshAiSkillsComboBox(selectedSkills ?? _aiSkills.FirstOrDefault());
    }

    private static AiSkillsTemplate CloneAiSkills(AiSkillsTemplate skills) => new()
    {
        Name = skills.Name,
        Prompt = skills.Prompt,
        UseWebSearch = skills.UseWebSearch,
        McpServers = skills.McpServers.Select(s => new McpServerEntry { Enabled = s.Enabled, Url = s.Url }).ToList(),
    };

    /// <summary>Rebinds <see cref="AiSkillsComboBox"/> to the current <see cref="_aiSkills"/> list and selects <paramref name="select"/> (or the first item).</summary>
    private void RefreshAiSkillsComboBox(AiSkillsTemplate? select)
    {
        AiSkillsComboBox.ItemsSource = null;
        AiSkillsComboBox.ItemsSource = _aiSkills;
        AiSkillsComboBox.DisplayMemberPath = nameof(AiSkillsTemplate.Name);
        AiSkillsComboBox.SelectedItem = select is not null && _aiSkills.Contains(select)
            ? select
            : _aiSkills.FirstOrDefault();

        UpdateAiSkillsPromptText();
        UpdateAiSkillsButtonsEnabled();
    }

    private void OnAiSkillsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateAiSkillsPromptText();
        UpdateAiSkillsButtonsEnabled();
    }

    private void UpdateAiSkillsPromptText()
        => AiSkillsPromptTextBox.Text = (AiSkillsComboBox.SelectedItem as AiSkillsTemplate)?.Prompt ?? string.Empty;

    /// <summary>Edit/delete only make sense when the list has at least one selected template.</summary>
    private void UpdateAiSkillsButtonsEnabled()
    {
        bool hasSelection = AiSkillsComboBox.SelectedItem is AiSkillsTemplate;
        AiSkillsEditButton.IsEnabled = hasSelection;
        AiSkillsDeleteButton.IsEnabled = hasSelection;
    }

    private void OnAiSkillsAddClick(object sender, RoutedEventArgs e)
    {
        var dialog = new AiSkillsDialog(template: null, otherSkillsNames: _aiSkills.Select(t => t.Name).ToList()) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _aiSkills.Add(dialog.Result);
            RefreshAiSkillsComboBox(dialog.Result);
        }
    }

    private void OnAiSkillsEditClick(object sender, RoutedEventArgs e)
    {
        if (AiSkillsComboBox.SelectedItem is not AiSkillsTemplate selected)
        {
            return;
        }

        var otherNames = _aiSkills.Where(t => !ReferenceEquals(t, selected)).Select(t => t.Name).ToList();
        var dialog = new AiSkillsDialog(selected, otherNames) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            if (string.Equals(
                _workingCopy.AiCapture.SelectedAiSkillsName,
                selected.Name,
                StringComparison.OrdinalIgnoreCase))
            {
                // Preserve the remembered overlay choice when that skill is renamed.
                _workingCopy.AiCapture.SelectedAiSkillsName = dialog.Result.Name;
            }

            var index = _aiSkills.IndexOf(selected);
            _aiSkills[index] = dialog.Result;
            RefreshAiSkillsComboBox(dialog.Result);
        }
    }

    private void OnAiSkillsDeleteClick(object sender, RoutedEventArgs e)
    {
        if (AiSkillsComboBox.SelectedItem is not AiSkillsTemplate selected)
        {
            return;
        }

        _aiSkills.Remove(selected);
        RefreshAiSkillsComboBox(_aiSkills.FirstOrDefault());
    }

    /// <summary>
    /// Resets the AI Skills list back to exactly the built-in templates, discarding
    /// edits to the built-ins as well as any user-added ones. This is destructive, so
    /// it's confirmed first; like every other change here it only reaches settings.json
    /// once the user saves.
    /// </summary>
    private void OnAiSkillsRestoreDefaultsClick(object sender, RoutedEventArgs e)
    {
        var defaults = AiCaptureSettings.CreateDefaultAiSkills();
        if (defaults.Count == 0)
        {
            // CreateDefaultAiSkills degrades to an empty list when the embedded asset is
            // missing or malformed. Resetting to nothing would be worse than not resetting.
            System.Windows.MessageBox.Show(this,
                "The built-in AI skills could not be loaded, so the list was left unchanged.",
                "SnipAgent", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = System.Windows.MessageBox.Show(this,
            "Reset the AI skills list to the built-in defaults?\n\n" +
            "Any skills you added, and any changes you made to the built-in skills, will be discarded.",
            "SnipAgent", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.OK)
        {
            return;
        }

        _aiSkills = defaults;
        RefreshAiSkillsComboBox(_aiSkills[0]);
    }

    private void OnBrowseFolderClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinFormsFolderBrowserDialog
        {
            Description = "Choose a folder to save screenshots to",
            SelectedPath = SaveFolderTextBox.Text,
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            SaveFolderTextBox.Text = dialog.SelectedPath;
        }
    }

    private void OnFilenamePatternTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        => UpdateFilenamePreview();

    private void UpdateFilenamePreview()
    {
        var pattern = string.IsNullOrWhiteSpace(FilenamePatternTextBox.Text)
            ? "Screenshot_{datetime}"
            : FilenamePatternTextBox.Text;

        var preview = ImageSaveService.BuildFileName(pattern, DateTime.Now);
        FilenamePreviewText.Text = $"Preview: {preview}.png";
    }

    private void OnHotkeyPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        var action = _hotkeyFields.First(pair => ReferenceEquals(pair.Value.Field, sender)).Key;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Tab && Keyboard.Modifiers is
            System.Windows.Input.ModifierKeys.None or System.Windows.Input.ModifierKeys.Shift)
        {
            e.Handled = false;
            return;
        }

        // Require at least one modifier so the hotkey doesn't hijack a plain key
        // used everywhere else in Windows.
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System)
        {
            return;
        }

        var modifiers = Keyboard.Modifiers;
        int modifierFlags = 0;
        if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control)) modifierFlags |= ModifierFlags.Control;
        if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt)) modifierFlags |= ModifierFlags.Alt;
        if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift)) modifierFlags |= ModifierFlags.Shift;
        if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Windows)) modifierFlags |= ModifierFlags.Win;

        if (modifierFlags == 0)
        {
            _hotkeyFields[action].Validation.Text = "Choose a combination that includes Ctrl, Alt, Shift, or Win.";
            return;
        }

        var candidate = new HotkeyDefinition
        {
            Modifiers = modifierFlags,
            VirtualKey = KeyInterop.VirtualKeyFromKey(key)
        };

        if (!candidate.IsValid())
        {
            _hotkeyFields[action].Validation.Text = "Choose a modifier and a valid non-modifier key.";
            return;
        }

        SetPendingHotkey(action, candidate);
    }

    private void OnRegisteredHotkeyPressed(object? sender, HotkeyActionEventArgs e)
    {
        foreach (var (action, controls) in _hotkeyFields)
        {
            if (controls.Field.IsKeyboardFocused)
            {
                // Windows may deliver WM_HOTKEY instead of a key-down for our own
                // active combination. Record it without starting a capture.
                e.Handled = true;
                SetPendingHotkey(action, e.Hotkey.Clone());
                return;
            }
        }
    }

    private void SetPendingHotkey(HotkeyAction action, HotkeyDefinition candidate)
    {
        _pendingHotkeys[action] = candidate;
        _hotkeyFields[action].Field.Text = candidate.ToString();
        HotkeySaveErrorText.Text = string.Empty;
        ValidatePendingHotkeys();
    }

    private IReadOnlyList<HotkeyFailure> ValidatePendingHotkeys()
    {
        foreach (var controls in _hotkeyFields.Values)
        {
            controls.Validation.Text = string.Empty;
        }

        var bindings = _pendingHotkeys.Select(pair => new HotkeyBinding(pair.Key, pair.Value)).ToArray();
        var failures = HotkeyBindings.Validate(bindings);
        foreach (var failure in failures)
        {
            _hotkeyFields[failure.Binding.Action].Validation.Text = failure.Reason;
        }
        return failures;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var failures = ValidatePendingHotkeys();
        if (failures.Count > 0)
        {
            HotkeySaveErrorText.Text = string.Join(Environment.NewLine, failures);
            return;
        }

        _workingCopy.SaveFolder = string.IsNullOrWhiteSpace(SaveFolderTextBox.Text)
            ? AppSettings.DefaultSaveFolder
            : SaveFolderTextBox.Text;
        _workingCopy.FilenamePattern = string.IsNullOrWhiteSpace(FilenamePatternTextBox.Text)
            ? "Screenshot_{datetime}"
            : FilenamePatternTextBox.Text;
        _workingCopy.Hotkey = _pendingHotkeys[HotkeyAction.ConfiguredLastUsed].Clone();
        _workingCopy.DedicatedHotkeys = new DedicatedHotkeySettings
        {
            AiSkills = _pendingHotkeys[HotkeyAction.RegionAiSkills].Clone(),
            ExtractText = _pendingHotkeys[HotkeyAction.RegionLlm].Clone(),
            FullScreen = _pendingHotkeys[HotkeyAction.FullScreen].Clone(),
            Region = _pendingHotkeys[HotkeyAction.Region].Clone()
        };
        _workingCopy.CaptureDelaySeconds = AppSettings.NormalizeCaptureDelay(
            CaptureDelayComboBox.SelectedValue is int seconds ? seconds : 0);
        var selectedAiMode = ExtractMethodComboBox.SelectedItem as AiModeChoice;
        _workingCopy.OcrEnabled = selectedAiMode?.IsEnabled == true;
        _workingCopy.Saving = SavingOptionComboBox.SelectedValue is SavingOption saving ? saving : SavingOption.SaveToFile;

        _workingCopy.AiCapture.Mode = selectedAiMode?.Mode ?? AiCaptureMode.None;
        _workingCopy.AiCapture.BaseUrl = string.IsNullOrWhiteSpace(AiBaseUrlTextBox.Text)
            ? AiCaptureSettings.DefaultBaseUrl
            : AiBaseUrlTextBox.Text.Trim();
        _workingCopy.AiCapture.Model = string.IsNullOrWhiteSpace(AiModelTextBox.Text)
            ? AiCaptureSettings.DefaultModel
            : AiModelTextBox.Text.Trim();
        _workingCopy.AiCapture.AiSkills = _aiSkills;

        // Only touch Credential Manager if the user actually typed a new key; an
        // empty box means "keep whatever is already saved" rather than "clear it".
        if (!string.IsNullOrEmpty(AiApiKeyBox.Password))
        {
            try
            {
                CredentialManagerService.SaveApiKey(AiApiKeyBox.Password);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this,
                    $"The API key could not be saved to Windows Credential Manager: {ex.Message}",
                    "SnipAgent", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        var result = _hotkeyManager.TryApply(_workingCopy, () => _settingsService.Save(_workingCopy));
        if (!result.Success)
        {
            HotkeySaveErrorText.Text = string.Join(Environment.NewLine, result.Errors);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
