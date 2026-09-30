# Additional global hotkeys

**Status:** Implementing
**Owner:** Repository owner
**Last reviewed:** 2026-09-30

## Problem

Starting a specific capture type or AI workflow currently requires opening the
tray menu or changing the configured AI Capture mode before using the single
global hotkey. This adds friction for users who frequently switch between region
capture, full-screen capture, LLM text extraction, and AI Skills.

## Desired outcome

Users can start each common workflow directly from anywhere in Windows with a
dedicated global shortcut:

- **Ctrl+Alt+A** starts a region capture using **Use AI Skills**.
- **Ctrl+Alt+C** starts a region capture using **Extract text (Using LLM)**.
- **Ctrl+Alt+F** starts a full-screen capture.
- **Ctrl+Alt+D** starts a region capture.

The existing configurable global hotkey continues to repeat the last-used
capture mode.

## In scope

- Register the four dedicated shortcuts when SnipAgent starts.
- Route each shortcut to its specified capture type.
- For Ctrl+Alt+A and Ctrl+Alt+C, override the configured AI Capture mode only
  for the capture started by that shortcut.
- Preserve the configured AI Capture mode for Ctrl+Alt+F, Ctrl+Alt+D, tray-menu
  captures, and the existing configurable hotkey.
- Keep successfully registered dedicated shortcuts active if another dedicated
  shortcut cannot be registered.
- Notify the user of every shortcut that could not be registered, identifying
  the unavailable key combination.
- Reserve the four dedicated combinations so they cannot be selected as the
  configurable hotkey.
- If persisted settings already use a reserved combination, keep all dedicated
  shortcuts and leave the conflicting configurable hotkey inactive while
  notifying the user.
- Continue ignoring duplicate shortcut requests while a capture is already in
  progress.
- Update current-behavior documentation and both user manuals.

## Out of scope

- Making the four new shortcuts configurable or allowing users to disable them.
- Adding dedicated shortcuts for Windows OCR, saving options, or individual AI
  Skills.
- Changing capture delay, target-selection overlays, save behavior, or the
  configured AI endpoint and selected AI Skill.
- Persisting the temporary AI Capture mode selected by Ctrl+Alt+A or Ctrl+Alt+C.
- Replacing `RegisterHotKey` with a low-level keyboard hook.

## Acceptance scenarios

1. **Given** SnipAgent is running and no capture is in progress
   **When** the user presses Ctrl+Alt+A
   **Then** region selection starts and the completed capture runs **Use AI
   Skills**, regardless of the configured AI Capture mode.

2. **Given** SnipAgent is running and no capture is in progress
   **When** the user presses Ctrl+Alt+C
   **Then** region selection starts and the completed capture runs **Extract
   text (Using LLM)**, regardless of the configured AI Capture mode.

3. **Given** the configured AI Capture mode has any value
   **When** a Ctrl+Alt+A or Ctrl+Alt+C capture completes or is canceled
   **Then** the configured AI Capture mode remains unchanged for subsequent
   captures.

4. **Given** SnipAgent is running and no capture is in progress
   **When** the user presses Ctrl+Alt+F
   **Then** full-screen monitor capture starts using the configured AI Capture
   mode and the existing monitor-selection behavior.

5. **Given** SnipAgent is running and no capture is in progress
   **When** the user presses Ctrl+Alt+D
   **Then** region selection starts using the configured AI Capture mode.

6. **Given** a capture is already in progress
   **When** the user presses any dedicated or configurable global shortcut
   **Then** no second capture workflow starts.

7. **Given** one or more dedicated shortcuts are owned by Windows or another
   application
   **When** SnipAgent starts
   **Then** every available dedicated shortcut remains functional and the user
   is notified which combinations could not be registered.

8. **Given** the user enters Ctrl+Alt+A, Ctrl+Alt+C, Ctrl+Alt+F, or Ctrl+Alt+D
   in the configurable hotkey field
   **When** SnipAgent validates the combination
   **Then** the combination is rejected as reserved and settings are not changed
   to that combination.

9. **Given** persisted settings already assign the configurable hotkey to one of
   the four reserved combinations
   **When** SnipAgent starts
   **Then** all available dedicated shortcuts are registered, the conflicting
   configurable hotkey remains inactive, and the notification identifies the
   conflict.

10. **Given** all five global shortcuts are available
    **When** SnipAgent starts and later exits
    **Then** it registers each shortcut with a distinct identifier and
    unregisters every registered shortcut during disposal.

## Constraints and assumptions

- Global shortcut handling continues to use the Win32 `RegisterHotKey` and
  `UnregisterHotKey` APIs through the hidden message window.
- The letters in the dedicated shortcuts refer to the physical virtual-key
  values A, C, F, and D with Ctrl and Alt modifiers.
- Region and full-screen captures retain the existing capture delay,
  freeze-before-selection behavior, remembered target, saving option, error
  handling, and capture-state persistence.
- Ctrl+Alt+A uses the currently selected AI Skill and existing AI Skills overlay
  behavior.
- Shortcut registration failures do not prevent SnipAgent from starting or tray
  capture commands from working.
- No architecture decision record is required because this extends the existing
  hotkey and capture orchestration boundaries without changing their underlying
  architecture.

## Implementation outline

Extend the hotkey manager to own multiple registrations with distinct IDs and
action-specific events while retaining registration probing for the Settings
window. Keep dedicated shortcut definitions and reserved-combination validation
in reusable non-UI logic.

Add capture entry points that accept an optional per-capture AI mode override.
Pass that override through the existing region capture pipeline without mutating
or saving `AppSettings.AiCapture.Mode`. Wire the dedicated actions during
application startup, report registration conflicts through the tray
notification surface, and dispose all successful registrations on exit.

## Tasks

- [ ] Register, dispatch, and unregister the four dedicated global shortcuts
      independently.
- [ ] Add region-capture entry points for one-capture AI mode overrides without
      mutating persisted settings.
- [ ] Reserve the dedicated combinations in Settings and handle conflicting
      persisted configurable hotkeys at startup.
- [ ] Add focused tests for shortcut definitions, reservation checks, dispatch,
      registration results, and non-persistent AI mode overrides where logic can
      be isolated from Windows UI interop.
- [ ] Update
      [FEATURES_AND_DEVELOPMENT.md](../FEATURES_AND_DEVELOPMENT.md),
      [USER_MANUAL.md](../USER_MANUAL.md), and
      [USER_MANUAL_KR.md](../USER_MANUAL_KR.md).
- [ ] Increment the application patch version exactly once.
- [ ] Run the repository verification script and complete manual Windows
      shortcut checks.

## Verification evidence

### Cloud or Ubuntu

Not run. Ubuntu compilation does not run the Windows-targeted tests.

### Windows automated

Not run. During implementation, run focused tests and then:

```powershell
.\scripts\verify.ps1
```

### Manual Windows

Pending:

- Verify Ctrl+Alt+A starts region selection and runs the selected AI Skill
  without changing the saved AI Capture mode.
- Verify Ctrl+Alt+C starts region selection and runs LLM text extraction
  without changing the saved AI Capture mode.
- Verify Ctrl+Alt+F starts the existing full-screen monitor flow.
- Verify Ctrl+Alt+D starts the existing region-selection flow.
- Verify the configurable hotkey still repeats the last-used capture mode.
- Verify rapid shortcut presses do not start overlapping captures.
- Verify a simulated registration conflict leaves the other shortcuts active
  and produces a notification naming the unavailable combination.
- Verify a reserved combination is rejected in Settings and from persisted
  settings.
- Verify exiting SnipAgent releases all registered shortcuts.

## Related decisions

- None.
