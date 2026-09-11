# Features and Development

Snipping Agent is a Windows-only screenshot capture tool built with .NET 10 and
WPF. It runs in the system tray without a main window.

## Features

- Runs as a system tray background app. Use the tray icon's right-click menu to
  access **Capture Region**, **Capture Full Screen**, **Settings**, and **Exit**.
- Region capture provides a dimmed overlay across all monitors. Drag to select,
  press Enter or release the mouse to confirm, and press Esc to cancel.
- Full-screen capture displays a numbered monitor picker when multiple monitors
  are connected. The picker is skipped when only one monitor is available.
- A global hotkey (default **Ctrl+Alt+S**, configurable in Settings) repeats the
  last region or full-screen capture mode. The previous region or monitor is
  shown so pressing **Enter** can recapture the same selection.
- An optional capture delay of 3, 5, or 10 seconds provides time to open a menu
  or display a tooltip before the desktop is frozen. It works with both capture
  modes and does not block the tray application.
- Screenshots are saved as PNG files in a configurable folder with a
  configurable filename pattern (default `Screenshot_{datetime}.png`).
  Collisions receive `_001`, `_002`, and subsequent suffixes. If the configured
  folder is unavailable, the application falls back to
  `%Pictures%\SnipAgent`.
- Optional OCR uses Windows' built-in `Windows.Media.Ocr` support and saves
  recognized text beside the image with the same base filename and a `.txt`
  extension. It requires an installed OCR language pack matching a Windows
  profile language. The image is still saved when OCR is unavailable or finds
  no text.
- Optional **Save to clipboard** places the result on the Windows clipboard
  instead of writing a file. When OCR is enabled, recognized text is copied;
  otherwise, the captured image is copied.
- Optional AI capture connects to an OpenAI-compatible endpoint configured with
  a base URL, model, and API key. The key is stored in Windows Credential
  Manager and never in `settings.json`.
  - **Use AI capture** extracts captured content as formatted Markdown.
  - **Use AI to answer** opens an always-on-top answer overlay where the user
    selects an AI skill and presses **Enter** to run it against the capture. The
    last selected skill is remembered. Press **Enter** or **Esc** to close the
    result; Esc also cancels a running request. The overlay is centered over the
    captured area and includes an opacity slider whose value is remembered in
    `settings.json`. Skills can optionally use hosted web search and MCP tool
    servers.
- Successful captures are silent. Windows notifications are shown only for
  failures such as hotkey conflicts, save-folder problems, and OCR errors.
- Settings are stored at `%AppData%\SnipAgent\settings.json`.

Additional design decisions, including the freeze-first capture model,
per-monitor DPI handling, and single-instance enforcement, are documented in
[PLAN.md](./PLAN.md).

## Project layout

```text
SnipAgent.slnx
src/
  SnipAgent.App/      # WPF tray app (net10.0-windows10.0.19041.0)
    Models/           # Settings, monitor, capture mode, and hotkey models
    Settings/         # JSON persistence and the Settings window
    Hotkeys/          # RegisterHotKey-based global hotkey manager
    Capture/          # Capture, monitor, save, OCR, AI, and credential services
    Overlays/         # Region, monitor picker, and AI answer overlays
    TrayIcon/         # Tray menu and Explorer-restart resilience
    Core/             # Capture orchestration
  SnipAgent.Tests/    # xUnit tests for pure application logic
```

## Building

The project requires the .NET 10 SDK.

```powershell
dotnet build
```

It targets `net10.0-windows10.0.19041.0` with WPF, WinForms, and the Windows 10
2004 WinRT API surface required by `Windows.Media.Ocr`.
`EnableWindowsTargeting=true` allows the project to be restored and compiled on
non-Windows systems for CI and editing. The application itself can run only on
Windows because it relies on WPF, WinForms, and Win32 interoperability,
including `RegisterHotKey`, `BitBlt`, and monitor enumeration.

`Microsoft.WindowsDesktop.App`, which supplies WPF and WinForms, is available
only on Windows. As a result, `dotnet run` and `dotnet test` fail on Linux and
macOS with a "No frameworks were found" error even when `dotnet build`
succeeds.

## Testing

Run the tests on Windows:

```powershell
dotnet test
```

The tests cover pure logic without invoking UI or platform interoperability:

- Filename pattern expansion and sanitization
- Settings JSON round-tripping and corruption recovery
- Coordinate calculations for cropping regions and monitors from the frozen
  desktop bitmap, including negative-coordinate multi-monitor layouts

## Known limitations

- No auto-start with Windows; launch the application manually.
- No post-capture notification on success (silent by design).
- UAC secure desktop and DRM-protected video content cannot be captured due to
  inherent GDI capture limitations.
