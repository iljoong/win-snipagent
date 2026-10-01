# Additional global hotkeys and customization

**Status:** Done
**Owner:** Repository owner
**Last reviewed:** 2026-10-01

**Tracking issue:** [#5](https://github.com/iljoong/win-snipagent/issues/5)

The fixed-shortcut feature was completed on 2026-09-30. This specification now
includes the approved customization follow-up. The earlier verification evidence
below applies only to the fixed-shortcut implementation, not this follow-up.

## Problem

SnipAgent has four dedicated global shortcuts, but their combinations are fixed.
Users cannot adapt them to their preferences or avoid conflicts with other apps.
Only the main, repeat-last-used global hotkey is currently customizable.

The full-screen shortcut currently uses the configured AI Capture mode. The
repository owner wants both dedicated capture-only shortcuts to produce images
without text extraction or AI.

## Desired outcome

Users can change the four dedicated shortcuts in **Settings > Advanced Options >
Custom Hotkey**, using the same click-and-press interaction as the main
**Global hotkey** field. Names remain short and simple:

| Option | Default | Action |
|--------|---------|--------|
| **AI Skills** | Ctrl+Alt+A | Region capture using the existing AI Skills workflow |
| **Extract Text** | Ctrl+Alt+C | Region capture with LLM text extraction |
| **Full Screen** | Ctrl+Alt+F | Monitor capture without text extraction or AI |
| **Region** | Ctrl+Alt+D | Region capture without text extraction or AI |

Brief descriptions sit beside bold option names, above their input fields:
AI Skills has **Region capture**, Extract Text has **Region capture using LLM**,
and Full Screen and Region have **Image only**. Descriptions use darker gray at
the normal text size and wrap on narrow layouts; only validation errors appear
below the input. Full action descriptions are also available to screen readers.
AI Skills refers to the existing
user-defined skills workflow, not a shortcut for each individual skill.

The main **Global hotkey** remains in **Capture Options**, defaults to Ctrl+Alt+S,
and continues to repeat the last-used capture type with the configured AI mode.

## In scope

- Add a **Custom Hotkey** tab alongside **AI Capture** and **AI Settings** under
  **Advanced Options**, containing the four fields above.
- Persist each dedicated combination independently and restore it on startup.
  Settings created before customization retain the four existing defaults and
  their existing main global hotkey.
- Keep all five fields editable with at least one Ctrl, Alt, Shift, or Win
  modifier and a non-modifier key. Display combinations consistently with the
  existing main field.
- Validate duplicate combinations across all five pending fields, rather than
  reserving the four default combinations forever. A freed default combination
  can be assigned to another action, including the main global hotkey.
- Permit editing a field to its own current combination and exchanging
  combinations between actions before Save. Temporary duplicate values may be
  shown while editing, but Save must reject a final duplicate assignment.
- Keep active shortcuts unchanged while editing. Save applies the complete
  pending set immediately; Cancel or closing without saving discards edits.
- If validation, registration, or persistence fails during Save, keep Settings
  open, surface the failure, and do not accept or persist the pending shortcut
  set. Restore the previous registrations if any were released for the attempt.
- Recheck Windows availability during Save, including races after an earlier
  availability check. Report action names and combinations for conflicts.
- At startup, keep available shortcuts functional if other shortcuts cannot
  register. Notify the user about each inactive action and combination.
- For dedicated Full Screen and Region captures, override the AI Capture mode
  to `None` for that capture only. AI Skills and Extract Text retain their
  respective one-capture overrides.
- Preserve the configured AI Capture mode for the main shortcut and tray
  captures; do not change it after any dedicated capture completes or cancels.
- Keep duplicate capture requests ignored while a capture is already running.
- Update current-behavior documentation and both user manuals upon implementation.

## Out of scope

- Disabling, clearing, or removing shortcuts; all five actions require a
  combination.
- Adding dedicated shortcuts for Windows OCR, saving options, or individual AI
  Skills.
- Changing capture delay, target-selection overlays, save behavior, or the
  configured AI endpoint and selected AI Skill.
- Moving or renaming the existing main Global hotkey option.
- Adding reset-to-default controls, shortcut presets, or multi-step key sequences.
- Persisting any temporary AI Capture mode selected by a dedicated shortcut.
- Replacing `RegisterHotKey` with a low-level keyboard hook.

## Acceptance scenarios

1. **Given** Settings is open
   **When** the user selects Advanced Options > Custom Hotkey
   **Then** AI Skills, Extract Text, Full Screen, and Region show their current
   combinations, each has its description beside its title above the input, and
   each accepts a combination by clicking and pressing keys.

2. **Given** a new installation or valid pre-customization settings
   **When** Settings loads
   **Then** the four dedicated fields default to Ctrl+Alt+A, Ctrl+Alt+C,
   Ctrl+Alt+F, and Ctrl+Alt+D respectively, without changing the main shortcut
   or unrelated settings.

3. **Given** five distinct, available pending combinations
   **When** the user selects Save and later restarts SnipAgent
   **Then** all five assignments work immediately and after restart, each
   triggers its assigned action, and replaced combinations no longer trigger
   their previous actions.

4. **Given** edited shortcut fields
   **When** the user selects Cancel or closes Settings without saving
   **Then** the active and persisted shortcuts remain unchanged.

5. **Given** any shortcut field is focused
   **When** the user presses a plain key or only modifier keys
   **Then** no invalid combination replaces the field; a plain key produces
   inline guidance to include a modifier.

6. **Given** two pending actions have the same combination
   **When** the user selects Save
   **Then** Settings stays open, identifies the conflicting actions and
   combination, and leaves active shortcuts and persisted settings unchanged.

7. **Given** the user keeps an existing combination, swaps two combinations,
   or assigns a freed default combination to the main shortcut
   **When** the final five assignments are distinct and available and Save is
   selected
   **Then** Save succeeds without treating SnipAgent's own registrations as an
   external conflict or treating the defaults as permanently reserved.

8. **Given** Windows or another app owns a proposed combination, including a
   combination claimed after an earlier check
   **When** Save attempts to apply the pending set
   **Then** Settings stays open, names the unavailable action and combination,
   does not persist the pending settings, and keeps or restores the old
   shortcuts. If Windows prevents restoring an old shortcut, the user is
   explicitly told which old action and combination is now inactive.

9. **Given** registering the pending set succeeds but writing settings fails
   **When** Save completes its failure path
   **Then** the error is visible, Settings stays open, the pending set is not
   reported as saved, and the previous shortcut registrations are restored
   with any restoration failures explicitly reported.

10. **Given** no capture is running and any configured AI Capture mode
    **When** the assigned AI Skills or Extract Text shortcut is pressed
    **Then** region capture starts the AI Skills workflow or LLM extraction
    respectively, and completion or cancellation leaves the saved mode unchanged.

11. **Given** no capture is running and any configured AI Capture mode
    **When** the assigned Full Screen or Region shortcut is pressed
    **Then** monitor capture or region selection starts respectively and
    produces an image without OCR, LLM extraction, or AI Skills; completion or
    cancellation leaves the saved mode unchanged.

12. **Given** a dedicated capture has completed or been canceled
    **When** the main shortcut or a tray capture is used
    **Then** the configured AI Capture mode still applies, and the main
    shortcut retains its existing repeat-last-used capture-type behavior.

13. **Given** a capture is already running
    **When** any global shortcut is pressed
    **Then** no overlapping capture starts.

14. **Given** persisted assignments conflict with another app, contain invalid
    key definitions, or duplicate another SnipAgent action
    **When** SnipAgent starts
    **Then** valid, available assignments remain active and each inactive
    action is identified in a notification without silently rewriting its
    assignment. Missing or null dedicated entries use their legacy defaults.

15. **Given** shortcuts have been changed and saved
    **When** SnipAgent exits
    **Then** every currently registered shortcut is released, and no stale
    registration from an earlier assignment remains.

## Constraints and assumptions

- Global shortcut handling continues to use the Win32 `RegisterHotKey` and
  `UnregisterHotKey` APIs through the hidden message window.
- Combinations remain modifier flags plus a Win32 virtual-key value, not
  locale-specific shortcut strings.
- Tab and Shift+Tab retain keyboard focus navigation in shortcut fields.
- All five assignments must be distinct. Startup retains the existing
  dedicated-first order (AI Skills, Extract Text, Full Screen, Region, then
  main) to resolve hand-edited duplicate assignments deterministically.
- Editing and availability probing must not release active registrations.
  Save may briefly release registrations to apply swaps and must handle
  rollback explicitly; Windows cannot guarantee ownership during that gap.
- Region and full-screen captures retain the existing capture delay,
  freeze-before-selection behavior, remembered target, saving option, error
  handling, and capture-state persistence.
- The AI Skills shortcut uses existing skill selection and overlay behavior.
- Shortcut registration failures do not prevent SnipAgent from starting or tray
  capture commands from working.
- No architecture decision record is required because this extends the existing
  hotkey and capture orchestration boundaries without changing their underlying
  architecture.

## Implementation outline

Extend [AppSettings](../../src/SnipAgent.App/Models/AppSettings.cs) and
[SettingsService](../../src/SnipAgent.App/Settings/SettingsService.cs) with
independent dedicated assignments and backward-compatible defaults. Reuse
[HotkeyDefinition](../../src/SnipAgent.App/Models/HotkeyDefinition.cs) for values
and display; replace fixed reservation checks with shared validation of the
complete pending set.

Extend [SettingsWindow](../../src/SnipAgent.App/Settings/SettingsWindow.xaml)
with the Custom Hotkey tab and reuse one key-entry/validation path across all
five fields. Keep changes in a working copy until Save. Coordinate registration
and persistence so Save failures restore the prior active set and do not accept
new settings. Make registration, persistence, and rollback failures testable
without interactive Windows UI.

Update [HotkeyManager](../../src/SnipAgent.App/Hotkeys/HotkeyManager.cs) and
[startup wiring](../../src/SnipAgent.App/App.xaml.cs) to register settings-backed
combinations under stable action IDs and report failures by action and
combination. Preserve independent startup registrations and disposal.

Reuse the existing optional per-capture mode override on the full-screen capture
path, passing `None` only for the dedicated Full Screen action. Leave tray and
main shortcut paths on the configured mode.

## Tasks

- [x] Make dedicated assignments settings-backed and verify defaults, old-settings
      migration, round trips, and independent startup conflict handling.
- [x] Deliver the Custom Hotkey tab with shared input handling, pending-set
      validation, own-combination reuse, swaps, immediate Save, and Cancel.
- [x] Verify failed Save and rollback with focused tests for duplicate,
      external-conflict, registration-race, persistence, and restoration failures.
- [x] Make dedicated Full Screen image-only and test mode isolation for all
      dedicated actions while preserving main/tray behavior.
- [x] Update existing hotkey tests to cover defaults and dynamic conflicts
      instead of permanent reservation, and test cleanup of changed registrations.
- [x] Update
      [FEATURES_AND_DEVELOPMENT.md](../FEATURES_AND_DEVELOPMENT.md),
      [USER_MANUAL.md](../USER_MANUAL.md), and
      [USER_MANUAL_KR.md](../USER_MANUAL_KR.md).
- [x] Increment the application patch version exactly once for implementation
      (0.1.2 to 0.1.3).
- [x] Run focused Windows tests and `.\scripts\verify.ps1`.
- [x] Record the repository owner's manual verification and acceptance below.

## Verification evidence

### Customization follow-up

Implemented locally on 2026-10-01 for [#5](https://github.com/iljoong/win-snipagent/issues/5).
The repository owner reported successful manual verification and accepted the
change on 2026-10-01.

#### Windows automated

Environment: Windows, .NET SDK 10.0.401.

Command:

```powershell
dotnet test .\src\SnipAgent.Tests\SnipAgent.Tests.csproj -c Debug --filter 'FullyQualifiedName~HotkeyDefinitionTests|FullyQualifiedName~HotkeyRegistrationSetTests|FullyQualifiedName~SettingsServiceTests'
```

Result: Passed. 81 tests passed, 0 failed, 0 skipped.

Command: `.\scripts\verify.ps1`

Result: Passed. Build: 0 warnings, 0 errors. Tests: 116 passed, 0 failed,
0 skipped.

PR handoff verification on 2026-10-01: a fresh `.\scripts\verify.ps1` attempt
was blocked by the running SnipAgent instance locking the Debug executable
(MSB3027/MSB3021). The instance was left running. Verification using separate
Release outputs passed:

```powershell
dotnet build .\SnipAgent.slnx -c Release
dotnet test .\SnipAgent.slnx -c Release --no-build
```

Results: build passed with 0 warnings and 0 errors; 116 tests passed, 0 failed,
0 skipped. No production code changed after the earlier successful Debug check.

The editor test tool did not discover the xUnit files; the explicit .NET command
above was used instead. Editor diagnostics reported no errors in the application
or tests.

Tests cover defaults and migration, all five persisted assignments, invalid and
duplicate combinations, mode routing, own-combination reuse, swaps, startup
partial failures, registration races, persistence failures, rollback and
restoration failures, a locked settings file retaining persisted assignments,
inactive-action repair, dispatch IDs, and cleanup.
Registration failure paths use an injected native-API test double, not claims
about interactive Win32 behavior.

#### Manual Windows

Result: Passed, as reported by the repository owner on 2026-10-01:
"I verified manually and it looks good."

No independent interactive validation was performed by the agent. The owner did
not provide individual scenario, monitor-configuration, DPI, or screen-reader
results; the following is the validation checklist, not a claim that each
configuration was separately exercised:

- Verify field names, key entry (including already-registered combinations),
  unchanged combinations, swaps, freed-default reuse, Save/Cancel/close,
  restart persistence, unavailable keys, and released registrations on exit.
- Exercise all four assigned actions with each configured AI mode, including
  cancellation, and verify main/tray behavior and no overlapping captures.
  Verify monitor selection on single- and multi-monitor setups.
- Verify inline errors, action/combination notifications, and accessible fields
  at the minimum Settings size and high DPI.

#### Inline-description refinement

Requested on 2026-10-01 after reviewing the supplied Settings screenshot.
Moved action descriptions beside the option titles, added a matching description
for AI Skills, improved text contrast and sizing, and provided full automation
help text. Input behavior and hotkey assignments are unchanged. Patch version:
0.1.3 to 0.1.4.

Automated verification: `.\scripts\verify.ps1` passed on 2026-10-01:
0 build warnings, 0 errors; 116 tests passed, 0 failed, 0 skipped.
PowerShell XML structure checks confirmed four bold titles with inline wrapping
descriptions and automation help text. `git diff --check` passed; editor XAML
diagnostics reported no errors.
Manual verification: the repository owner reported the updated UI looks good on
2026-10-01. Narrow-layout, high-DPI, and screen-reader results were not separately
reported.

#### Cloud or Ubuntu

Not run; implementation and automated validation were local on Windows.

### Historical fixed-shortcut implementation

The following results are retained from 2026-09-30. They do not verify
customization, Save rollback, or the new image-only Full Screen behavior.

#### Cloud or Ubuntu

Command: `bash ./scripts/verify-ubuntu.sh`

Environment: Ubuntu runner, .NET SDK 10.0.401.

Result: Passed on 2026-09-30. The Windows-targeted solution compiled successfully.

Warnings: 0.

Errors: 0.

This is compile-only evidence; Windows tests and interactive APIs were not run in
this environment.

#### Windows automated

Command: `.\scripts\verify.ps1`

Environment: Windows, repository owner local validation at PR head
`008ca33d10c9092530189b794659a6080663a352`, 2026-09-30.

Result: Passed.

Build: 0 warnings, 0 errors.

Tests: 65 passed, 0 failed, 0 skipped, 65 total.

```powershell
.\scripts\verify.ps1
```

#### Manual Windows

Result: Passed on 2026-09-30, as reported by the repository owner. Every listed
manual scenario passed:

- Ctrl+Alt+A starts region selection and runs the selected AI Skill without
  changing the saved AI Capture mode.
- Ctrl+Alt+C starts region selection and runs LLM text extraction without
  changing the saved AI Capture mode.
- Ctrl+Alt+F starts the existing full-screen monitor flow.
- Ctrl+Alt+D starts a plain region-selection flow without OCR, LLM extraction,
  or AI Skills, while leaving the configured AI Capture mode unchanged.
- The configurable hotkey repeats the last-used capture mode.
- Rapid shortcut presses do not start overlapping captures.
- A registration conflict leaves the other shortcuts active and produces a
  notification naming the unavailable combination.
- Reserved combinations are rejected in Settings and from persisted settings.
- Exiting SnipAgent releases all registered shortcuts.

## Related decisions

- None.
