using System.Drawing;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using SnipAgent.App.Capture;
using SnipAgent.App.Models;
using SnipAgent.App.Overlays.Markdown;
using SnipAgent.App.Settings;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace SnipAgent.App.Overlays;

/// <summary>
/// Displays the result of "Use AI to answer" mode as an always-on-top overlay
/// instead of saving the capture. Lets the user choose an AI skill before starting,
/// then updates the text in place once the answer arrives (or an error occurs).
/// </summary>
public partial class AiAnswerOverlayWindow : Window
{
    private const int WM_DPICHANGED = 0x02E0;

    private enum OverlayState
    {
        Ready,
        Running,
        Finished
    }

    private readonly Bitmap _bitmap;
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly System.Drawing.Rectangle _targetBounds;
    private double _lastSavedOpacity;
    private double _lastSavedWidth;
    private double _lastSavedHeight;
    private string? _lastSavedAiSkillsName;
    private CancellationTokenSource? _answerCancellation;
    private OverlayState _state;
    private bool _isClosing;
    private bool _hasBeenPositionedByUser;
    private readonly AiAnswerMarkdownPresenter _markdownPresenter;

    /// <summary>
    /// The answer produced by the AI call, captured so the caller can also save it
    /// (per the saving option) after the overlay is dismissed. Null if the call failed
    /// or the overlay was closed before the answer arrived.
    /// </summary>
    public string? AnswerResult { get; private set; }

    private AiAnswerOverlayWindow(Bitmap bitmap, AppSettings settings, SettingsService settingsService,
        System.Drawing.Rectangle targetBounds)
    {
        InitializeComponent();

        _bitmap = bitmap;
        _settings = settings;
        _settingsService = settingsService;
        _targetBounds = targetBounds;
        _markdownPresenter = new AiAnswerMarkdownPresenter(AiAnswerMarkdownPresenter.OpenHttpLinkInDefaultBrowser);
        MinWidth = AppSettings.MinAiAnswerOverlayWidth;
        MinHeight = AppSettings.MinAiAnswerOverlayHeight;
        _lastSavedWidth = settings.AiAnswerOverlayWidth;
        _lastSavedHeight = settings.AiAnswerOverlayHeight;
        _lastSavedAiSkillsName = settings.AiCapture.SelectedAiSkillsName;

        ApplyMonitorSizeConstraints(
            targetBounds,
            AppSettings.NormalizeAiAnswerOverlayWidth(settings.AiAnswerOverlayWidth),
            AppSettings.NormalizeAiAnswerOverlayHeight(settings.AiAnswerOverlayHeight));

        // Restore the remembered overlay opacity; setting the slider value drives
        // OnOpacityChanged, which applies it to the panel background brush.
        _lastSavedOpacity = AppSettings.NormalizeAiAnswerOverlayOpacity(settings.AiAnswerOverlayOpacity);
        OpacitySlider.Value = _lastSavedOpacity;
        PanelBackgroundBrush.Opacity = _lastSavedOpacity;

        AiSkillsComboBox.ItemsSource = settings.AiCapture.AiSkills;
        AiSkillsComboBox.DisplayMemberPath = nameof(AiSkillsTemplate.Name);
        AiSkillsComboBox.SelectedItem = AiCaptureService.ResolveSelectedAiSkills(settings.AiCapture);

        if (AiSkillsComboBox.SelectedItem is null)
        {
            AiSkillsComboBox.IsEnabled = false;
            ShowPlainTextMessage("No AI skills are configured. Add an AI skill in Settings before using this mode.");
            InteractionHint.Text = "Press Esc to close";
        }

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        // Use PreviewKeyDown so Enter/Esc still control overlay lifecycle even when
        // focus is inside selectable Markdown controls.
        PreviewKeyDown += OnKeyDown;
        Closed += OnClosed;
    }

    private void OnOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Fires once during XAML parse (before fields are assigned); ignore until ready.
        if (_settings is null || PanelBackgroundBrush is null)
        {
            return;
        }

        PanelBackgroundBrush.Opacity = e.NewValue;
        _settings.AiAnswerOverlayOpacity = e.NewValue;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _isClosing = true;
        _answerCancellation?.Cancel();
        CaptureOverlaySize();

        // Persist any unsaved overlay preference or skill selection. The normal path
        // saves the selected skill when execution starts; this also retries if that
        // save failed and captures the final resized dimensions.
        if (Math.Abs(_settings.AiAnswerOverlayOpacity - _lastSavedOpacity) < 0.0001 &&
            Math.Abs(_settings.AiAnswerOverlayWidth - _lastSavedWidth) < 0.0001 &&
            Math.Abs(_settings.AiAnswerOverlayHeight - _lastSavedHeight) < 0.0001 &&
            string.Equals(
                _settings.AiCapture.SelectedAiSkillsName,
                _lastSavedAiSkillsName,
                StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            _settingsService.Save(_settings);
            _lastSavedOpacity = _settings.AiAnswerOverlayOpacity;
            _lastSavedWidth = _settings.AiAnswerOverlayWidth;
            _lastSavedHeight = _settings.AiAnswerOverlayHeight;
            _lastSavedAiSkillsName = _settings.AiCapture.SelectedAiSkillsName;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to persist AI answer overlay settings: {ex.Message}");
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // Center the overlay within the selected region (or captured monitor)
        // rather than always on the primary screen, using physical pixels so it
        // lands correctly regardless of per-monitor DPI scaling.
        var hwnd = new WindowInteropHelper(this).Handle;

        // Watch for WM_DPICHANGED so we can re-assert our centered position after WPF's
        // built-in per-monitor-DPI handling reacts (see ApplyCenteredPosition / WndProc).
        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);

        ApplyCenteredPosition(hwnd);

        // Same reasoning as the other overlays: this is triggered from a background
        // tray app, so it needs to force itself to the foreground to receive
        // keyboard input (Enter/Esc) without a preceding click.
        NativeWindowPositioning.ForceForeground(hwnd);
        Activate();
        Focus();
        Keyboard.Focus(AiSkillsComboBox.IsEnabled ? AiSkillsComboBox : this);
    }

    /// <summary>
    /// Re-centers the overlay over <see cref="_targetBounds"/> by setting only its
    /// top-left position in physical pixels. WPF continues to own the remembered DIP
    /// size so moving between monitors cannot compound DPI scaling.
    /// </summary>
    private void ApplyCenteredPosition(IntPtr hwnd)
    {
        var bounds = NativeWindowPositioning.GetCenteredPhysicalBounds(
            _targetBounds, Width, Height);
        NativeWindowPositioning.SetWindowPositionPhysicalPixels(hwnd, bounds.Left, bounds.Top);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_DPICHANGED)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var windowBounds = NativeWindowPositioning.GetWindowBoundsPhysicalPixels(hwnd);
                ApplyMonitorSizeConstraints(
                    windowBounds.IsEmpty ? _targetBounds : windowBounds,
                    Width,
                    Height);

                if (!_hasBeenPositionedByUser)
                {
                    ApplyCenteredPosition(hwnd);
                }
            }), DispatcherPriority.Loaded);
        }

        return IntPtr.Zero;
    }

    private void OnDragHandleMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _hasBeenPositionedByUser = true;
        DragMove();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Re-center now that any WM_DPICHANGED-driven adjustment from
        // SourceInitialized's initial move has already settled (see ApplyCenteredPosition).
        if (!_hasBeenPositionedByUser)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            ApplyCenteredPosition(hwnd);
        }
    }

    private void OnResizeDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (sender is not Thumb { Tag: string direction })
        {
            return;
        }

        _hasBeenPositionedByUser = true;

        bool resizeLeft = direction.Contains("Left", StringComparison.Ordinal);
        bool resizeRight = direction.Contains("Right", StringComparison.Ordinal);
        bool resizeTop = direction.Contains("Top", StringComparison.Ordinal);
        bool resizeBottom = direction.Contains("Bottom", StringComparison.Ordinal);

        if (resizeLeft || resizeRight)
        {
            double oldWidth = ActualWidth;
            double desiredWidth = oldWidth + (resizeLeft ? -e.HorizontalChange : e.HorizontalChange);
            double newWidth = Math.Clamp(desiredWidth, MinWidth, MaxWidth);
            if (resizeLeft)
            {
                Left += oldWidth - newWidth;
            }
            Width = newWidth;
        }

        if (resizeTop || resizeBottom)
        {
            double oldHeight = ActualHeight;
            double desiredHeight = oldHeight + (resizeTop ? -e.VerticalChange : e.VerticalChange);
            double newHeight = Math.Clamp(desiredHeight, MinHeight, MaxHeight);
            if (resizeTop)
            {
                Top += oldHeight - newHeight;
            }
            Height = newHeight;
        }
    }

    private void ApplyMonitorSizeConstraints(
        System.Drawing.Rectangle monitorTargetBounds,
        double desiredWidth,
        double desiredHeight)
    {
        var workArea = NativeWindowPositioning.GetMonitorWorkAreaSizeDip(monitorTargetBounds);
        MaxWidth = Math.Max(MinWidth, workArea.WidthDip);
        MaxHeight = Math.Max(MinHeight, workArea.HeightDip);
        Width = Math.Clamp(
            AppSettings.NormalizeAiAnswerOverlayWidth(desiredWidth),
            MinWidth,
            MaxWidth);
        Height = Math.Clamp(
            AppSettings.NormalizeAiAnswerOverlayHeight(desiredHeight),
            MinHeight,
            MaxHeight);
    }

    private void CaptureOverlaySize()
    {
        _settings.AiAnswerOverlayWidth = AppSettings.NormalizeAiAnswerOverlayWidth(ActualWidth);
        _settings.AiAnswerOverlayHeight = AppSettings.NormalizeAiAnswerOverlayHeight(ActualHeight);
    }

    private void RequestClose()
    {
        Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RequestClose();
    }

    private async Task StartSelectedSkillAsync()
    {
        if (_state != OverlayState.Ready ||
            AiSkillsComboBox.SelectedItem is not AiSkillsTemplate selectedSkills)
        {
            return;
        }

        _state = OverlayState.Running;
        AiSkillsComboBox.IsEnabled = false;
        InteractionHint.Text = "Running... Press Esc to cancel";

        _settings.AiCapture.SelectedAiSkillsName = selectedSkills.Name;
        var persistenceWarning = PersistSettings();
        ShowPlainTextMessage(persistenceWarning is null
            ? "Thinking..."
            : $"Thinking...\n\n{persistenceWarning}");

        var cancellation = new CancellationTokenSource();
        _answerCancellation = cancellation;
        try
        {
            var answer = await AiCaptureService.AnswerAsync(
                _bitmap, _settings, selectedSkills, cancellation.Token);
            if (_isClosing)
            {
                return;
            }

            AnswerResult = answer;
            if (string.IsNullOrWhiteSpace(answer))
            {
                ShowPlainTextMessage("(No answer returned.)");
            }
            else
            {
                ShowMarkdownAnswer(answer);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (!_isClosing)
            {
                ShowPlainTextMessage("AI request canceled.");
            }
        }
        catch (Exception ex)
        {
            if (!_isClosing)
            {
                ShowPlainTextMessage($"AI capture failed: {ex.Message}");
            }
        }
        finally
        {
            if (ReferenceEquals(_answerCancellation, cancellation))
            {
                _answerCancellation = null;
            }
            cancellation.Dispose();

            if (!_isClosing)
            {
                _state = OverlayState.Finished;
                InteractionHint.Text = "Press Enter or Esc to close";
            }
        }
    }

    private void ShowMarkdownAnswer(string answer)
    {
        try
        {
            AnswerMarkdownViewer.Document = _markdownPresenter.Render(answer);
            AnswerMarkdownViewer.Visibility = Visibility.Visible;
            AnswerTextScrollViewer.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to render markdown answer: {ex.Message}");
            ShowPlainTextMessage($"Warning: Markdown formatting failed. Showing plain text.\n\n{answer}");
        }
    }

    private void ShowPlainTextMessage(string message)
    {
        AnswerMarkdownViewer.Document = new System.Windows.Documents.FlowDocument();
        AnswerMarkdownViewer.Visibility = Visibility.Collapsed;
        AnswerTextScrollViewer.Visibility = Visibility.Visible;
        AnswerText.Text = message;
    }

    private string? PersistSettings()
    {
        CaptureOverlaySize();

        try
        {
            _settingsService.Save(_settings);
            _lastSavedOpacity = _settings.AiAnswerOverlayOpacity;
            _lastSavedWidth = _settings.AiAnswerOverlayWidth;
            _lastSavedHeight = _settings.AiAnswerOverlayHeight;
            _lastSavedAiSkillsName = _settings.AiCapture.SelectedAiSkillsName;
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to persist AI answer overlay settings: {ex.Message}");
            return $"Warning: the selected AI skill could not be remembered: {ex.Message}";
        }
    }

    private async void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            RequestClose();
            return;
        }

        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        if (_state == OverlayState.Ready)
        {
            await StartSelectedSkillAsync();
        }
        else if (_state == OverlayState.Finished)
        {
            RequestClose();
        }
    }

    /// <summary>
    /// Shows the overlay and blocks (pumping the message loop, like the other
    /// overlays) until the user selects a skill, starts it with Enter, and dismisses
    /// the result. <paramref name="targetBounds"/>
    /// is the selected region's (or captured monitor's) bounds in physical pixels,
    /// used to center the overlay over the relevant area of the screen. Returns the
    /// AI answer text (or null if it failed / was dismissed early) so the caller can
    /// save it alongside the extracted text.
    /// </summary>
    public static string? ShowAnswer(Bitmap bitmap, AppSettings settings, SettingsService settingsService,
        System.Drawing.Rectangle targetBounds)
    {
        var overlay = new AiAnswerOverlayWindow(bitmap, settings, settingsService, targetBounds);
        overlay.ShowDialog();
        return overlay.AnswerResult;
    }
}
