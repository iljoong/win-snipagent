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
- Dedicated global shortcuts are configurable under **Advanced Options >
  Custom Hotkey**: **AI Skills** (default **Ctrl+Alt+A**) starts region capture
  with AI Skills, **Extract Text** (**Ctrl+Alt+C**) starts region capture with
  LLM extraction, **Full Screen** (**Ctrl+Alt+F**) starts monitor capture, and
  **Region** (**Ctrl+Alt+D**) starts region capture. Full Screen and Region
  always produce images without OCR or AI extraction. Each dedicated action
  overrides the AI mode for that capture only; the main shortcut and tray
  commands still use the configured mode.
  Each field shows a bold option name with a short inline description above its
  input, including **Image only** for capture-only actions. Description text uses
  darker gray at the normal text size and wraps when needed; validation errors
  remain below the input. Screen readers receive full action help text.
- All five hotkeys require modifier+key combinations and distinct assignments.
  Their defaults are not permanently reserved: unchanged combinations, swaps,
  and reuse of freed defaults are supported. Editing leaves live registrations
  intact, and focused hotkey fields record SnipAgent's own shortcuts without
  triggering captures. Save validates and registers the complete set before
  persisting; registration or write failures retain settings and restore prior
  registrations, reporting any rollback failures explicitly. Cancel discards
  edits. Startup registers available assignments independently and reports each
  invalid, duplicate, or unavailable action and combination.
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
    last selected skill is remembered. Press **Enter** after the answer to
    continue saving or copying; **Esc** at any point discards the entire capture
    and cancels a running request. The upper-right close button retains its
    existing close-and-continue behavior (and cancels a running request).
    The overlay is centered over the captured area, can be moved
    and resized from its edges or corners, and remembers its size. It also includes
    an opacity slider whose value is remembered in `settings.json`. Completed
    answers render as selectable dark-themed Markdown in native WPF, while
    ready/running/canceled/failed/empty states stay as plain text. Only
    user-activated absolute HTTP(S) links can open in the default browser;
    relative/non-HTTP(S) links remain inert text; images are replaced with
    blocked-image alt-text indicators; and raw HTML is rendered as literal text.
    Skills can optionally use hosted web search and MCP tool servers.
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

## Versioning and releases

The application version is stored in the `<Version>` property in
`src/SnipAgent.App/SnipAgent.App.csproj`. Release tags use the same semantic
version prefixed with `v`, such as `v0.1.0`.

Agent-authored features and fixes increment the patch component once per
complete change. Major and minor versions change only when explicitly requested
by the repository owner. Documentation-only, test-only, and workflow-only
changes do not increment the application version unless they accompany a
feature or fix.

Create a self-contained Windows x64 build locally with:

```powershell
dotnet publish .\src\SnipAgent.App\SnipAgent.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o .\Out\win-x64
```

To publish a GitHub Release, commit the intended version, create a matching
annotated tag, and push the commit and tag:

```powershell
git tag -a v0.1.0 -m "SnipAgent 0.1.0"
git push origin main
git push origin v0.1.0
```

The release workflow rejects a tag that does not exactly match the project
version. After verification, it publishes a self-contained `win-x64` ZIP and a
SHA-256 checksum to the GitHub Release. Increment the project version before
creating each subsequent release tag.

## Testing

Run the tests on Windows:

```powershell
dotnet test
```

Before completing a change, run the repository verification entry point, which
builds the solution and executes the full automated test suite:

```powershell
.\scripts\verify.ps1
```

The `CI` GitHub Actions workflow runs this same command on `windows-latest` for
every pull request and push to `main`, and can also be started manually. This is
the authoritative automated CI gate for the Windows-targeted solution.

On Ubuntu, including the default Copilot cloud-agent environment, run the
compile-only gate:

```bash
bash ./scripts/verify-ubuntu.sh
```

It restores and builds the Windows-targeted solution with isolated artifacts,
but it does not run tests and does not replace the Windows CI workflow, local
Windows verification, or required manual checks.

The tests cover pure logic without invoking UI or platform interoperability:

- Filename pattern expansion and sanitization
- Settings JSON round-tripping and corruption recovery
- Coordinate calculations for cropping regions and monitors from the frozen
  desktop bitmap, including negative-coordinate multi-monitor layouts

## Agent-assisted development

[AGENTS.md](../AGENTS.md) is the canonical repository guide for coding agents.
Medium or multi-session changes use lightweight
[feature specifications](./specs/README.md), while architecturally significant
decisions use append-only [architecture decision records](./adr/README.md).
Reusable project skills under `.github/skills/` help create and maintain those
documents without requiring them for small changes. Cloud delegation uses the
`cloud-execution` skill and the
[Copilot cloud execution guide](./CLOUD_EXECUTION.md); cloud work stays on an
agent-owned task branch and draft PR until Windows validation and human review
are complete.

## Known limitations

- No auto-start with Windows; launch the application manually.
- No post-capture notification on success (silent by design).
- UAC secure desktop and DRM-protected video content cannot be captured due to
  inherent GDI capture limitations.
