using System.Threading;
using System.Windows;
using SnipAgent.App.Core;
using SnipAgent.App.Hotkeys;
using SnipAgent.App.Models;
using SnipAgent.App.Settings;
using SnipAgent.App.TrayIcon;

namespace SnipAgent.App;

/// <summary>
/// Application entry point. SnipAgent is tray-only (ShutdownMode=OnExplicitShutdown
/// in App.xaml, no StartupUri/main window). Enforces a single running instance via
/// a named mutex: a second launch signals the first instance to open Settings, then
/// exits immediately rather than registering a second (conflicting) global hotkey
/// and tray icon.
/// </summary>
public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = "SnipAgent.SingleInstance.Mutex";
    private const string ShowSettingsEventName = "SnipAgent.ShowSettings.Event";

    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _showSettingsEvent;

    private SettingsService? _settingsService;
    private HotkeyManager? _hotkeyManager;
    private TrayIconManager? _trayIconManager;
    private CaptureController? _captureController;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        _showSettingsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSettingsEventName);

        if (!createdNew)
        {
            // Another instance is already running: ask it to open Settings, then exit.
            _showSettingsEvent.Set();
            Shutdown();
            return;
        }

        _settingsService = new SettingsService();
        _hotkeyManager = new HotkeyManager();
        _trayIconManager = new TrayIconManager();
        _captureController = new CaptureController(_settingsService, _trayIconManager);

        _trayIconManager.CaptureRegionRequested += (_, _) => _captureController.CaptureRegion();
        _trayIconManager.CaptureFullScreenRequested += (_, _) => _captureController.CaptureFullScreen();
        _trayIconManager.SettingsRequested += (_, _) => OpenSettings();
        _trayIconManager.ExitRequested += (_, _) => Shutdown();

        _hotkeyManager.HotkeyPressed += (_, args) =>
        {
            switch (args.Action)
            {
                case HotkeyAction.RegionAiSkills:
                    _captureController.CaptureRegion(HotkeyActionRouting.GetRegionAiModeOverride(args.Action));
                    break;
                case HotkeyAction.RegionLlm:
                    _captureController.CaptureRegion(HotkeyActionRouting.GetRegionAiModeOverride(args.Action));
                    break;
                case HotkeyAction.FullScreen:
                    _captureController.CaptureFullScreen();
                    break;
                case HotkeyAction.Region:
                    _captureController.CaptureRegion(HotkeyActionRouting.GetRegionAiModeOverride(args.Action));
                    break;
                default:
                    _captureController.CaptureLastUsedMode();
                    break;
            }
        };

        var initialSettings = _settingsService.Load();
        foreach (var (action, hotkey) in _hotkeyManager.RegisterAll(initialSettings.Hotkey))
        {
            _trayIconManager.ShowFailureNotification(
                "Hotkey unavailable",
                action == HotkeyAction.ConfiguredLastUsed
                    ? $"SnipAgent's hotkey ({hotkey}) is reserved or already in use by another app."
                    : $"The dedicated shortcut ({hotkey}) is already in use by another app.");
        }

        // Listen for a second-instance launch requesting Settings be shown.
        RegisterWaitForShowSettingsSignal();
    }

    private void RegisterWaitForShowSettingsSignal()
    {
        if (_showSettingsEvent is null)
        {
            return;
        }

        ThreadPool.RegisterWaitForSingleObject(
            _showSettingsEvent,
            (_, _) => Dispatcher.Invoke(OpenSettings),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    private void OpenSettings()
    {
        if (_settingsService is null || _hotkeyManager is null)
        {
            return;
        }

        var window = new SettingsWindow(_settingsService, _hotkeyManager)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        window.Activate();
        window.ShowDialog();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyManager?.Dispose();
        _trayIconManager?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
