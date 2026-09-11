# SnipAgent — Implementation Plan and Design Record

**Status: Implemented.** The application described below is built in
`src/SnipAgent.App/` and covered by focused tests in `src/SnipAgent.Tests/`.
This document records the current scope, architecture, completed work, and remaining
real-hardware validation.

For user instructions, see [USER_MANUAL.md](./USER_MANUAL.md) or
[USER_MANUAL_KR.md](./USER_MANUAL_KR.md).

## 1. Product Goal

Build a Windows-only screenshot utility using C#/.NET 10 and WPF that:

- Runs quietly as a system tray application with no main window.
- Captures a selected region or one monitor.
- Works correctly across mixed-DPI, multi-monitor desktops.
- Repeats the last capture mode through a configurable global hotkey.
- Routes images and extracted text to files, the clipboard, both, or neither.
- Optionally extracts text with Windows OCR or an OpenAI-compatible model.
- Optionally runs reusable AI Skills against a capture and displays the answer in an
  overlay.

## 2. Implemented User Experience

### Application lifecycle

- Windows-only WPF application targeting `net10.0-windows10.0.19041.0`.
- Tray-only background process with no main window or normal taskbar entry.
- Single-instance enforcement through a named mutex.
- A second launch signals the running instance to open Settings, then exits.
- Tray menu:
  - **Capture Region**
  - **Capture Full Screen**
  - **Settings**
  - **Exit**
- Double-clicking the tray icon opens Settings.
- The tray icon is restored after Windows Explorer broadcasts `TaskbarCreated`.
- The application does not start automatically with Windows.

### Capture modes

#### Region capture

- Optional countdown runs before the desktop is frozen.
- The virtual desktop is captured into one bitmap before the selection UI appears.
- A dimmed overlay spans all monitors.
- Dragging selects a physical-pixel rectangle.
- Mouse release or **Enter** confirms; **Esc** cancels.
- Accidental very small selections keep the overlay open for another attempt.
- The last successful region is shown when the overlay next opens, allowing **Enter**
  to recapture it.

#### Full-screen capture

- A single-monitor system captures that monitor without showing a picker.
- A multi-monitor system displays a numbered picker over the frozen desktop.
- A monitor can be selected by mouse, number key, or numeric keypad key.
- **Esc** cancels.
- The last successful monitor is highlighted and can be recaptured with **Enter**.

### Capture delay

- Settings offers **Off**, **3 seconds**, **5 seconds**, and **10 seconds**.
- The countdown gives the user time to expose transient UI such as menus and tooltips.
- The desktop is frozen when the countdown ends, so neither the countdown nor the
  selection overlay appears in the result.
- The delay applies to both region and full-screen capture.

### Global hotkey

- One global hotkey, default **Ctrl+Alt+S**.
- Implemented with `RegisterHotKey`/`UnregisterHotKey`, not a low-level keyboard hook.
- Repeats the last successfully completed capture mode.
- The remembered region or monitor is preselected by the corresponding overlay.
- Remapping requires at least one modifier: Ctrl, Alt, Shift, or Win.
- A candidate combination is test-registered before it is accepted.
- If final registration fails after saving, the Settings window reports the failure.
- Reentrant requests are ignored while a capture or answer flow is active.

### Saving and clipboard routing

The saving destination is independent of the extraction method:

- **Save to file**
- **Save to clipboard**
- **Save to file and clipboard**
- **Off**

File behavior:

- Image format is PNG only.
- Default save folder is `%Pictures%\SnipAgent`.
- Default filename pattern is `Screenshot_{datetime}`.
- Supported UI tokens are `{date}`, `{time}`, and `{datetime}`.
- `{timestamp}` remains supported for settings-file compatibility.
- Invalid filename characters and reserved Windows names are sanitized.
- Collisions receive `_001`, `_002`, and later suffixes.
- If the configured folder is unavailable, the image is saved to
  `%Pictures%\SnipAgent` and the user is notified of the fallback.

Clipboard behavior:

- Without extraction, the captured image is copied.
- With extraction enabled, extracted text is copied instead of the image.
- Empty extraction output produces a notification and does not replace the clipboard.
- Clipboard access failures are surfaced through a notification.

Selecting **Off** stores nothing, but the AI Skills answer overlay can still run.

### Text extraction

Settings exposes four choices:

1. **None**
   - No extraction.
   - File saving produces only the PNG.
2. **Extract text (Windows OCR)**
   - Uses `Windows.Media.Ocr`.
   - Requires an installed OCR language matching a Windows user language.
   - Saves a `.txt` sidecar when text is recognized.
3. **Extract text (Using LLM)**
   - Sends the PNG to an OpenAI-compatible image-capable model.
   - Uses the embedded Markdown extraction prompt.
   - Saves a `.md` sidecar.
4. **Use AI Skills**
   - Opens the answer overlay over the selected region or monitor.
   - Runs the user-selected prompt against the capture.
   - When file saving is enabled, saves extracted Markdown and the answer together in
     a `.md` sidecar under `Extracted text` and `Answer` headings.

Extraction failures do not prevent the PNG from being saved.

### AI endpoint and credential handling

- Configurable OpenAI-compatible Base URL and model/deployment name.
- Default Base URL: `https://api.openai.com/v1`.
- Default model: `gpt-5.6-sol`.
- API key is stored in Windows Credential Manager, never in `settings.json`.
- Leaving the API Key field blank preserves an existing credential.
- Entering a new key replaces the stored credential.

### AI Skills

- Named prompt templates stored in application settings.
- Built-in defaults are loaded from an embedded JSON resource.
- Settings supports previewing, adding, editing, and deleting Skills.
- **Restore defaults** replaces edits and additions after confirmation.
- Skill names must be present and unique, case-insensitively.
- Each Skill can optionally enable:
  - Hosted web search.
  - One or more remote MCP servers.
- MCP servers are restricted to enabled absolute HTTP(S) endpoint URLs.
- Unreachable or invalid MCP servers are skipped so the base answer can still run.

### AI answer overlay

- Always-on-top, fixed-size overlay centered on the captured region or monitor.
- Correctly repositions across monitors with different DPI settings.
- The title can be dragged to move the overlay manually.
- The user chooses an AI Skill, then presses **Enter** to run it.
- **Esc** closes the overlay and cancels an active request.
- **Enter** or **Esc** closes the overlay after completion.
- The last executed Skill is remembered.
- Background opacity can be adjusted from 20% to 100% and is persisted in
  `settings.json`.
- Failures and cancellation are shown in the overlay instead of being presented as a
  successful answer.

### Feedback

- Successful captures do not produce completion toasts or sounds.
- A temporary **Saving…** overlay is shown while extraction and destination routing
  run on a background STA thread.
- Windows notification balloons report operational failures, including:
  - Initial hotkey conflicts.
  - Save-folder fallback.
  - Save or extracted-text write failures.
  - OCR/LLM extraction failures.
  - Empty text when clipboard text was requested.
  - Clipboard access failures.

## 3. Technical Architecture

### Capture pipeline

1. Reject the request if another capture is active.
2. Load settings and enumerate monitors.
3. Show the configured countdown.
4. Capture the physical-pixel virtual desktop with GDI `BitBlt`.
5. Display the region or monitor-selection overlay over the frozen bitmap.
6. Crop the selected physical-pixel bounds from the frozen bitmap.
7. If AI Skills is selected, display the answer overlay and collect its result.
8. Show the saving-progress overlay while extracting and routing the result.
9. Save the successful capture mode, region, or monitor for the next hotkey action.
10. Dispose bitmaps, native handles, clients, windows, and cancellation resources.

### Key design decisions

- **Freeze before selection:** prevents overlays from contaminating screenshots and
  eliminates selection-to-recapture races.
- **GDI capture:** `BitBlt` is mature and sufficient for the intended desktop capture
  scope. Secure-desktop and DRM limitations are accepted and documented.
- **Physical-pixel coordinates:** capture, monitor, crop, and native positioning math
  avoids mixed-DPI errors caused by treating WPF DIPs as screen pixels.
- **Per-Monitor DPI Aware V2:** declared in the application manifest.
- **Negative coordinates supported:** virtual desktop and crop math do not assume the
  primary monitor begins at `(0,0)`.
- **Background STA saving:** keeps the tray UI responsive while preserving compatibility
  with Windows clipboard APIs.
- **Credential separation:** secrets are stored in Credential Manager while ordinary
  preferences remain in JSON.
- **Behavior-safe extraction:** extraction errors are reported but do not discard a
  capturable image.

## 4. Current Project Structure

```text
SnipAgent.slnx
src/
  SnipAgent.App/
    App.xaml(.cs)                    # Startup, single instance, component wiring
    app.manifest                     # DPI awareness and execution level
    Assets/                          # Icon, Markdown prompt, default AI Skills
    Models/                          # Settings, capture, saving, AI, monitor models
    Settings/                        # JSON persistence and Settings/Skill dialogs
    Hotkeys/                         # RegisterHotKey-based global hotkey manager
    Capture/                         # GDI capture, OCR, AI, clipboard, file, credentials
    Overlays/                        # Countdown, selection, answer, saving progress
    TrayIcon/                        # Tray menu, notifications, Explorer resilience
    Core/                            # End-to-end capture orchestration
  SnipAgent.Tests/
    AiCaptureServiceTests.cs
    ImageSaveServiceFileNameTests.cs
    ScreenCaptureServiceCropTests.cs
    SettingsServiceTests.cs
docs/
  USER_MANUAL.md
  USER_MANUAL_KR.md
  PLAN.md
```

## 5. Persistence

General settings are stored at:

```text
%AppData%\SnipAgent\settings.json
```

The settings service:

- Creates defaults when the file is missing.
- Recovers safely from malformed or incompatible JSON.
- Normalizes unsupported capture-delay values.
- Clamps answer-overlay opacity to the supported range.
- Restores missing nested AI settings and empty endpoint/model values to defaults.

The API key is stored separately in Windows Credential Manager.

## 6. Test Coverage

The xUnit test project covers logic that does not require interactive UI:

- Filename token expansion, sanitization, reserved names, and collision suffixes.
- Settings serialization, round-tripping, normalization, and corruption recovery.
- Region and monitor crop calculations, including negative-coordinate layouts.
- AI Markdown prompt loading and AI Skill selection behavior.

Build and test commands:

```powershell
dotnet build
dotnet test
```

The application and tests target Windows desktop APIs. Restore and compilation can use
`EnableWindowsTargeting=true` outside Windows, but running the app and executing the
tests requires Windows because `Microsoft.WindowsDesktop.App` is not available on
Linux or macOS.

## 7. Completed Milestones

- [x] Project scaffold and Windows application manifest
- [x] Single-instance tray application lifecycle
- [x] Settings model, persistence, defaults, and recovery
- [x] Configurable global hotkey with conflict validation
- [x] GDI virtual-desktop capture and monitor enumeration
- [x] Multi-monitor region selection
- [x] Numbered monitor picker
- [x] Last-region and last-monitor recall
- [x] Configurable capture countdown
- [x] PNG saving, filename rules, collision handling, and fallback folder
- [x] File, clipboard, combined, and off routing
- [x] Windows OCR extraction and `.txt` sidecars
- [x] OpenAI-compatible Markdown extraction and `.md` sidecars
- [x] Credential Manager API-key storage
- [x] AI Skills templates, defaults, web search, and remote MCP tools
- [x] AI answer overlay with cancellation, movement, and remembered opacity
- [x] Non-blocking saving-progress feedback
- [x] Failure notifications
- [x] Focused unit tests
- [x] English and Korean user manuals

## 8. Remaining Validation and Risks

No unimplemented product feature is currently tracked in this plan. The remaining work
is manual validation on representative Windows hardware:

- [ ] Mixed-DPI multi-monitor combinations such as 100%/150%/200%.
- [ ] Portrait monitors and non-rectangular monitor arrangements.
- [ ] Negative-coordinate layouts with the primary monitor away from the virtual
  desktop origin.
- [ ] Explorer restart and tray-icon restoration.
- [ ] Monitor hot-plug or unplug while a selection overlay is open.
- [ ] Clipboard contention with applications that hold the clipboard open.
- [ ] Windows OCR across multiple installed user-language packs.
- [ ] OpenAI-compatible providers with different image-input and Responses API support.
- [ ] Hosted web search behavior for supported and unsupported models.
- [ ] Streamable HTTP and SSE MCP servers, including unreachable endpoints.
- [ ] Long-running AI requests, cancellation, and overlay movement across DPI boundaries.
- [ ] Save-folder loss, permission failures, low disk space, and long paths.

Known platform limitations:

- UAC secure desktop cannot be captured.
- DRM-protected video may appear black.
- PNG is the only image format.
- Auto-start with Windows is not provided.
- AI features depend on the configured provider, model, network, and provider policies.
