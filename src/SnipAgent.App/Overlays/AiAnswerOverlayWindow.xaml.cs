using System.Drawing;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using SnipAgent.App.Capture;
using SnipAgent.App.Models;
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
    private string? _lastSavedAiSkillsName;
    private CancellationTokenSource? _answerCancellation;
    private OverlayState _state;
    private bool _isClosing;
    private bool _hasBeenMovedByUser;

    // The overlay's intended size in device-independent (WPF) units, captured from
    // XAML before the window is shown. Used to compute the centered position; WPF
    // itself owns the actual (DPI-scaled) window size, so these never change even as
    // the window moves between monitors of differing DPI.
    private readonly double _intendedWidthDip;
    private readonly double _intendedHeightDip;

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
        _intendedWidthDip = Width;
        _intendedHeightDip = Height;
        _lastSavedAiSkillsName = settings.AiCapture.SelectedAiSkillsName;

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
            AnswerText.Text = "No AI skills are configured. Add an AI skill in Settings before using this mode.";
            InteractionHint.Text = "Press Esc to close";
        }

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
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

        // Persist any unsaved opacity or skill selection. The normal path saves the
        // selected skill when execution starts; this also retries if that save failed.
        if (Math.Abs(_settings.AiAnswerOverlayOpacity - _lastSavedOpacity) < 0.0001 &&
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
    /// top-left position in physical pixels — never its size. This window has a fixed
    /// DIP size (760x520) that WPF renders at the correct physical size for whichever
    /// monitor it currently occupies, so we must let WPF own sizing. Earlier attempts
    /// that also set the physical *size* desynced WPF's DIP model: moving the window
    /// onto a monitor with a different DPI fires WM_DPICHANGED, and WPF re-scales the
    /// window by the DPI ratio — compounding with our manual size and shrinking (or
    /// growing) the overlay on every transition. By repositioning only, WPF keeps the
    /// size correct and we just keep it centered. The centered top-left is derived
    /// from the intended DIP size scaled by the *target* monitor's DPI (via
    /// <see cref="NativeWindowPositioning.GetCenteredPhysicalBounds"/>), matching the
    /// physical size WPF will render there. Re-applied after WM_DPICHANGED settles
    /// (from <see cref="WndProc"/>) and in <see cref="OnLoaded"/> to correct the
    /// proportional reposition WPF applies during the DPI transition.
    /// </summary>
    private void ApplyCenteredPosition(IntPtr hwnd)
    {
        var bounds = NativeWindowPositioning.GetCenteredPhysicalBounds(
            _targetBounds, _intendedWidthDip, _intendedHeightDip);
        NativeWindowPositioning.SetWindowPositionPhysicalPixels(hwnd, bounds.Left, bounds.Top);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_DPICHANGED && !_hasBeenMovedByUser)
        {
            // Let WPF apply its DPI-driven resize/reposition first, then re-center the
            // (correctly WPF-sized) window over the target once that has settled.
            // Repositioning within the same monitor does not fire another
            // WM_DPICHANGED, so this cannot loop.
            Dispatcher.BeginInvoke(new Action(() => ApplyCenteredPosition(hwnd)), DispatcherPriority.Loaded);
        }

        return IntPtr.Zero;
    }

    private void OnDragHandleMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _hasBeenMovedByUser = true;
        DragMove();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Re-center now that any WM_DPICHANGED-driven adjustment from
        // SourceInitialized's initial move has already settled (see ApplyCenteredPosition).
        var hwnd = new WindowInteropHelper(this).Handle;
        ApplyCenteredPosition(hwnd);
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
        AnswerText.Text = persistenceWarning is null
            ? "Thinking..."
            : $"Thinking...\n\n{persistenceWarning}";

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
            AnswerText.Text = string.IsNullOrWhiteSpace(answer) ? "(No answer returned.)" : answer;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (!_isClosing)
            {
                AnswerText.Text = "AI request canceled.";
            }
        }
        catch (Exception ex)
        {
            if (!_isClosing)
            {
                AnswerText.Text = $"AI capture failed: {ex.Message}";
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

    private string? PersistSettings()
    {
        try
        {
            _settingsService.Save(_settings);
            _lastSavedOpacity = _settings.AiAnswerOverlayOpacity;
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
            Close();
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
            Close();
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
