using System.IO;
namespace SnipAgent.App.Models;

/// <summary>
/// All user-configurable settings, persisted as JSON at
/// %AppData%\SnipAgent\settings.json. Kept as a simple POCO so it round-trips
/// cleanly through System.Text.Json.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Folder screenshots are saved to. Falls back to %Pictures%\SnipAgent if invalid at save time.</summary>
    public string SaveFolder { get; set; } = DefaultSaveFolder;

    /// <summary>
    /// Filename pattern (without extension; .png is always appended). Supports the
    /// tokens: {date} (yyyyMMdd), {time} (HHmmss), {datetime} (yyyyMMdd_HHmmss),
    /// {timestamp} (yyyyMMdd_HHmmss - same as {datetime}), {counter} (reserved for
    /// future use). Unknown tokens are left as-is.
    /// </summary>
    public string FilenamePattern { get; set; } = "Screenshot_{datetime}";

    /// <summary>The capture mode the global hotkey should repeat.</summary>
    public CaptureMode LastCaptureMode { get; set; } = CaptureMode.Region;

    /// <summary>
    /// The last region selected in a region capture, in physical-pixel virtual-desktop
    /// coordinates. Pre-shown in the region overlay so Enter re-captures it. Null until
    /// the user has completed at least one region capture.
    /// </summary>
    public CaptureRegion? LastRegion { get; set; }

    /// <summary>
    /// Device name (e.g. \\.\DISPLAY1) of the monitor chosen in the last full-screen
    /// capture. Pre-highlighted in the monitor picker so Enter re-captures it. Null
    /// until the user has completed at least one full-screen capture.
    /// </summary>
    public string? LastMonitorDeviceName { get; set; }

    /// <summary>The user-configurable global hotkey. Defaults to Ctrl+Alt+S.</summary>
    public HotkeyDefinition Hotkey { get; set; } = HotkeyDefinition.Default;

    /// <summary>
    /// Countdown, in seconds, between confirming the region/monitor and taking the
    /// actual screenshot. Lets the user set up a transient UI state (open a menu,
    /// hover a tooltip) before the shot fires. 0 means capture immediately. Only the
    /// values in <see cref="SupportedCaptureDelays"/> are valid; any other value is
    /// normalized back to 0 on load (see <see cref="NormalizeCaptureDelay"/>).
    /// </summary>
    public int CaptureDelaySeconds { get; set; }

    /// <summary>
    /// Legacy compatibility flag for settings written before
    /// <see cref="AiCaptureMode.None"/> was introduced. New code uses
    /// <see cref="AiCaptureSettings.Mode"/> as the source of truth.
    /// </summary>
    public bool OcrEnabled { get; set; }

    /// <summary>
    /// Where capture results are stored (file, clipboard, both, or nowhere). This is
    /// independent of the text-extraction engine: whatever is produced (image or
    /// extracted text) is routed according to this option. Defaults to
    /// <see cref="SavingOption.SaveToFile"/>.
    /// </summary>
    public SavingOption Saving { get; set; } = SavingOption.SaveToFile;

    /// <summary>
    /// Settings for the "AI capture" feature group (OpenAI-compatible Markdown
    /// extraction / question answering). The API key is stored separately in
    /// Windows Credential Manager, not here.
    /// </summary>
    public AiCaptureSettings AiCapture { get; set; } = new();

    /// <summary>
    /// Opacity (0.2–1.0) of the AI answer overlay's background panel. Adjusted live
    /// via a slider on the overlay itself and remembered here; deliberately NOT
    /// surfaced in the settings window. Defaults to 0.93 (the original #EE alpha).
    /// Clamped to <see cref="MinAiAnswerOverlayOpacity"/>–1.0 on load so the overlay
    /// can never become fully invisible.
    /// </summary>
    public double AiAnswerOverlayOpacity { get; set; } = DefaultAiAnswerOverlayOpacity;

    /// <summary>
    /// Remembered AI answer overlay dimensions in WPF device-independent pixels.
    /// They are normalized on load and constrained to the target monitor when the
    /// overlay opens.
    /// </summary>
    public double AiAnswerOverlayWidth { get; set; } = DefaultAiAnswerOverlayWidth;
    public double AiAnswerOverlayHeight { get; set; } = DefaultAiAnswerOverlayHeight;

    public const double DefaultAiAnswerOverlayOpacity = 0.93;
    public const double MinAiAnswerOverlayOpacity = 0.2;
    public const double DefaultAiAnswerOverlayWidth = 760;
    public const double DefaultAiAnswerOverlayHeight = 520;
    public const double MinAiAnswerOverlayWidth = 480;
    public const double MinAiAnswerOverlayHeight = 320;

    /// <summary>Clamps an arbitrary (possibly hand-edited) overlay opacity into the supported range.</summary>
    public static double NormalizeAiAnswerOverlayOpacity(double value) =>
        double.IsNaN(value) ? DefaultAiAnswerOverlayOpacity : Math.Clamp(value, MinAiAnswerOverlayOpacity, 1.0);

    public static double NormalizeAiAnswerOverlayWidth(double value) =>
        NormalizeAiAnswerOverlayDimension(value, MinAiAnswerOverlayWidth, DefaultAiAnswerOverlayWidth);

    public static double NormalizeAiAnswerOverlayHeight(double value) =>
        NormalizeAiAnswerOverlayDimension(value, MinAiAnswerOverlayHeight, DefaultAiAnswerOverlayHeight);

    private static double NormalizeAiAnswerOverlayDimension(double value, double minimum, double defaultValue) =>
        double.IsFinite(value) && value >= minimum ? value : defaultValue;

    /// <summary>The delay values the UI offers and the only ones treated as valid. 0 = Off.</summary>
    public static readonly IReadOnlyList<int> SupportedCaptureDelays = new[] { 0, 3, 5, 10 };

    /// <summary>
    /// Coerces an arbitrary (possibly hand-edited or corrupted) delay value to a
    /// supported one, falling back to 0 (immediate capture) for anything unsupported.
    /// </summary>
    public static int NormalizeCaptureDelay(int value)
        => SupportedCaptureDelays.Contains(value) ? value : 0;

    public static string DefaultSaveFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "SnipAgent");

    public static string FallbackSaveFolder => DefaultSaveFolder;
}
