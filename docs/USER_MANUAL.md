# SnipAgent User Manual

SnipAgent is a screen capture tool that runs in the Windows system tray.
It has no main window and is controlled through its tray icon. Captures can be
saved as PNG files or copied to the Windows clipboard. It also supports text
extraction and AI-powered answers using Windows OCR or an OpenAI-compatible model.

---

## 1. Getting Started

### Requirements

- Windows (SnipAgent is a Windows-only application)
- .NET 10 Desktop Runtime

### Running SnipAgent

- Run the executable. No window opens; instead, the SnipAgent icon appears in the
  **system tray (the notification area on the right side of the taskbar)**.
- Only one instance can run at a time. If you run the executable while SnipAgent is
  already running, the existing application's **Settings** window opens and the new
  instance exits.
- SnipAgent does not start automatically with Windows. Run it manually when needed.
- If the tray icon is not visible, select the **Show hidden icons (∧)** button on the
  taskbar.

---

## 2. Basic Usage

Right-click the tray icon to open the menu.

| Menu | Description |
|------|-------------|
| **Capture Region** | Capture an area selected by dragging |
| **Capture Full Screen** | Capture one monitor's entire screen |
| **Settings** | Configure saving, capture, text extraction, and AI features |
| **Exit** | Exit the application |

Double-clicking the tray icon also opens the Settings window.

SnipAgent does not display a completion notification after a successful capture.
Windows notifications appear only for issues that require attention, such as hotkey
conflicts, save failures, or text extraction failures. A **Saving…** progress window
appears while OCR or AI processing takes time.

---

## 3. Region Capture

1. Select **Capture Region** from the tray menu, or press the global hotkey when the
   last-used mode is region capture.
2. If a capture delay is configured, the countdown appears first.
3. When the countdown ends, SnipAgent freezes the entire desktop and displays a
   darkened selection overlay across all monitors.
4. Drag the mouse over the area you want to capture.
5. Release the mouse button, or press **Enter** after selecting an area, to confirm.
6. Press **Esc** to cancel.

> **Remembering the last region:** The previously captured region is shown as a blue
> rectangle. Press **Enter** without drawing a new region to capture the same area
> again. Drawing a new region also updates the remembered region.

> A very small drag is treated as an accidental click. The capture is not confirmed,
> and the overlay remains open so you can select the region again.

---

## 4. Full-Screen Capture

- With **one monitor**, SnipAgent captures that monitor immediately without showing
  a monitor picker.
- With **multiple monitors**, a numbered selection overlay appears on every monitor.
  - Click the monitor you want to capture, or
  - press its **number key (1–9 on the main keyboard or numeric keypad)**.
  - Press **Esc** to cancel.

If a capture delay is configured, the countdown appears before the monitor picker.
The screen is frozen at the moment the countdown ends.

> **Remembering the last monitor:** The previously captured monitor is highlighted
> with a gold border and a `(last)` label. Press **Enter** to capture that monitor
> again.

---

## 5. Global Hotkey

- The default hotkey is **Ctrl + Alt + S**.
- The hotkey repeats the last-used capture mode: region or full screen.
- It is a Windows global hotkey and works while you are using other applications.
- Duplicate hotkey or capture requests are ignored while a capture is already in
  progress.

When you start a capture with the hotkey, the previously captured region or monitor
is preselected. Press **Enter** to capture the same target again, or draw a new region
or select another monitor.

---

## 6. Settings

Select **Save** at the bottom of the Settings window to apply your changes. Select
**Cancel** to close the window without applying changes made during that session.

### 6.1 Save Option

#### Save folder

- Specifies the folder where PNG screenshots are saved.
- Select **Browse…** to choose a folder.
- The default folder is `Pictures\SnipAgent`.
- If the configured folder has been deleted or cannot be accessed, SnipAgent
  automatically saves to the default folder and displays a Windows notification with
  the actual saved location.
- This setting has no effect when the selected saving option does not save files.

#### File name pattern

- The `.png` extension is appended automatically.
- The default pattern is `Screenshot_{datetime}`.
- The **Preview** in the Settings window shows the resulting file name.

| Token | Meaning | Example |
|-------|---------|---------|
| `{datetime}` | Date and time (`yyyyMMdd_HHmmss`) | `20260911_190125` |
| `{date}` | Date (`yyyyMMdd`) | `20260911` |
| `{time}` | Time (`HHmmss`) | `190125` |

If you edit `settings.json` directly, `{timestamp}` is also supported and produces the
same value as `{datetime}`. Unknown tokens are left unchanged.

- Characters that Windows does not allow in file names are replaced with `_`.
- If a file with the same name already exists, SnipAgent appends `_001`, `_002`, and
  so on.
- Extracted text files use the same base name as the PNG file.

### 6.2 Capture Options

#### Global hotkey

- Select the hotkey field, then press the desired key combination.
- The combination must include at least one of **Ctrl, Alt, Shift, or Win**.
- A combination already used by Windows or another application cannot be registered.
- Selecting **Save** immediately registers the new hotkey.

#### Capture delay

Choose one of the following values:

| Setting | Behavior |
|---------|----------|
| **Off** | Freeze the capture screen immediately |
| **3 seconds** | Freeze the screen after a 3-second countdown |
| **5 seconds** | Freeze the screen after a 5-second countdown |
| **10 seconds** | Freeze the screen after a 10-second countdown |

During the countdown, you can open a menu, hover over a tooltip, or otherwise prepare
the desired screen state. The delay applies to both region and full-screen captures,
and the countdown itself is not included in the result.

#### Saving option

This setting controls where capture results are sent, independently of the selected
text extraction method.

| Setting | File | Clipboard |
|---------|------|-----------|
| **Save to file** | Save the PNG and extracted text | Do not copy |
| **Save to clipboard** | Do not save | Copy the image or extracted text |
| **Save to file and clipboard** | Save files | Also copy to the clipboard |
| **Off** | Do not save | Do not copy |

- When text extraction is set to **None**, the captured image is copied to the
  clipboard.
- When text extraction is enabled, the extracted text is copied instead of the image.
- If extraction returns no text, nothing is copied and a notification is displayed.
- With **Off**, the `Use AI Skills` answer overlay still appears, but no result is
  saved.

### 6.3 Advanced Options > AI Capture

Use the list at the top of the tab to select the text extraction method that runs
after a capture.

| Selection | Behavior | Additional result when saving to a file |
|-----------|----------|-----------------------------------------|
| **None** | Do not extract text | Save only the PNG |
| **Extract text (Windows OCR)** | Extract plain text with Windows OCR | A `.txt` file with the same name |
| **Extract text (Using LLM)** | Extract structured Markdown with an OpenAI-compatible model | A `.md` file with the same name |
| **Use AI Skills** | Run the selected AI Skill against the capture and display its answer | A `.md` file containing the extracted content and answer |

For example, Windows OCR produces
`Screenshot_20260911_190125.png` and `Screenshot_20260911_190125.txt`.
LLM-based methods use the `.md` extension for the extracted text file.

#### Windows OCR

- Windows OCR uses an installed OCR language pack matching one of your Windows user
  languages.
- If no appropriate language pack is installed or no text is found in the image, no
  text file is created.
- An OCR failure does not prevent the PNG from being saved.

#### Managing AI Skills

Selecting `Use AI Skills` enables the AI Skills section.

- Select a Skill from the list to preview its prompt below.
- Select the pencil button to edit the selected Skill.
- Select `+` to add a Skill.
- Select the trash button to delete the selected Skill.
- **Restore defaults** discards user-added Skills and edits to built-in Skills, then
  restores the built-in list. You must approve the confirmation dialog before the
  reset is applied.

The add/edit Skill window provides the following settings:

- **Skills Name**: A unique name for the Skill
- **Skills Prompt**: The question or instruction to apply to the captured image
- **Use web search tool**: Allow the model to use its hosted web search tool
- **MCP server**: A list of remote MCP servers whose tools may be used

MCP servers must be absolute `http://` or `https://` endpoint URLs. Use the checkbox
to the left of each server to enable or disable it, select `+` to add a row, or select
`×` to remove one.

### 6.4 Advanced Options > AI Settings

Configure the following fields before using LLM-based extraction or AI Skills:

- **Base URL**: The OpenAI-compatible endpoint URL
  - Default: `https://api.openai.com/v1`
- **Model**: The model or deployment name
  - Default: `gpt-5.6-sol`
- **API Key**: The authentication key for the endpoint

The API key is stored securely in **Windows Credential Manager**, not in the
plain-text `settings.json` file. If a key is already saved, leave the API Key field
blank to keep it. Entering a new value replaces the existing key.

---

## 7. Using the AI Answer Overlay

When you capture with `Use AI Skills` selected, an always-on-top answer overlay opens
in the center of the selected region or monitor.

1. Select the Skill to run from the **AI Skill** list at the top.
2. Press **Enter** to run it.
3. The overlay displays `Running...` while the request is in progress.
4. When the answer appears, review it and press **Enter** or **Esc** to close the
   overlay.

- Press **Esc** before starting to close the overlay without sending a request.
- Press **Esc** while a request is running to cancel the request and close the
  overlay.
- The last Skill you ran is remembered and preselected the next time the overlay
  opens.
- Drag the `AI answer` heading to move the overlay.
- Use the **Opacity** slider in the lower-left corner to set the background opacity
  from 20% to 100%. You can also focus the slider and use the arrow keys.
- The last opacity value is stored in `settings.json` and applied the next time the
  overlay opens. It cannot be changed from the Settings window.

When file saving is enabled, the AI Skills `.md` result contains both the Markdown
extracted from the capture and the Skill's answer, under the `Extracted text` and
`Answer` headings.

---

## 8. Settings File

General settings are stored as JSON at:

```text
%AppData%\SnipAgent\settings.json
```

The API key is not included in this file. It is stored separately in Windows
Credential Manager. Using the Settings window is recommended instead of editing the
settings file directly.

---

## 9. Troubleshooting

| Symptom | Cause / Solution |
|---------|------------------|
| The hotkey does not work | Windows or another application may already be using the combination. Choose another hotkey in Settings. |
| A save-folder notification appears | The configured folder could not be used, so the capture was saved to `Pictures\SnipAgent`. Choose a valid folder in Settings. |
| The tray icon is not visible | Check the hidden-icons area. The icon is restored automatically if Windows Explorer restarts. |
| Part of the screen is captured as black | UAC secure desktops and DRM-protected video cannot be captured. |
| Windows OCR is enabled, but no `.txt` file is created | The image may contain no recognizable text, or the required OCR language pack may not be installed. Check the language features under **Language & region** in Windows Settings. |
| An authentication or request error occurs when using an AI feature | Verify the Base URL, Model, and API Key, and confirm that the selected model supports image input. |
| The AI Skills list is empty | Add a Skill in Settings or select **Restore defaults** to restore the built-in list. |
| Nothing is copied to the clipboard | When text extraction is enabled and returns no text, SnipAgent does not copy anything. Set extraction to **None** if you want to copy the image instead. |

---

## 10. Limitations

- SnipAgent does not start automatically with Windows.
- It does not display a completion notification after a successful capture.
- PNG is the only supported image file format.
- UAC secure desktops and DRM-protected content cannot be captured.
- In multi-monitor environments, SnipAgent handles per-monitor scaling and virtual
  desktop coordinates, but it cannot save content that Windows blocks from capture.
- LLM and AI Skills features depend on the capabilities, model, network availability,
  and usage policies of the configured external OpenAI-compatible endpoint.
