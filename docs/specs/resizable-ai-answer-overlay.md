# Resizable AI answer overlay

**Status:** Done
**Owner:** Repository owner
**Last reviewed:** 2026-09-12

## Problem

The AI answer overlay has a fixed 760 x 520 size and can be dismissed only with
the keyboard. Long answers may require excessive scrolling, and users who prefer
mouse interaction do not have a visible way to close the overlay.

## Desired outcome

Users can resize the AI answer overlay to suit the displayed answer and close it
with an obvious mouse-accessible control. The overlay remembers its last valid
size for future captures without changing its existing movement, keyboard,
cancellation, opacity, or answer-saving behavior.

## In scope

- Allow the frameless AI answer overlay to be resized from its edges and corners.
- Keep the answer area responsive so it gains or loses usable space as the
  overlay is resized.
- Enforce a minimum size of 480 x 320 device-independent pixels so the Skill
  selector, answer, interaction hint, opacity control, and close control remain
  usable.
- Persist the most recently used valid width and height in `settings.json`.
- Use 760 x 520 device-independent pixels when no saved size exists.
- Normalize invalid, non-finite, or undersized saved dimensions and keep the
  restored window within the usable bounds of its target monitor.
- Add a close button at the upper-right of the overlay.
- Make the close button follow the same state-dependent behavior as **Esc**:
  close without starting a request in the ready state, cancel and close while a
  request is running, and close while preserving a completed answer in the
  finished state.
- Preserve resizing and close behavior across supported per-monitor DPI settings.
- Update current-behavior documentation and both user manuals.

## Out of scope

- Remembering the overlay's screen position.
- Adding maximize, minimize, restore, or taskbar controls.
- Changing the initial centering or manual title-drag behavior.
- Changing the answer font, rendering format, opacity range, or Skill selector.
- Changing **Enter** or **Esc** keyboard behavior.
- Making other application overlays resizable.

## Acceptance scenarios

1. **Given** the overlay is open at its default or restored size
   **When** the user drags any edge or corner
   **Then** the window resizes in that direction, never becomes smaller than
   480 x 320 device-independent pixels, and the answer viewport uses the
   available space.

2. **Given** the user resized the overlay to a valid size
   **When** the overlay is closed and opened for a later capture
   **Then** the new overlay restores that width and height in
   device-independent pixels and initially centers over the new capture target.

3. **Given** saved overlay dimensions are missing, non-finite, corrupted, or
   smaller than the supported minimum
   **When** settings are loaded and the overlay opens
   **Then** the dimensions are normalized to valid values, using the 760 x 520
   defaults when the saved values cannot be used.

4. **Given** the saved overlay size is larger than the usable bounds of the
   target monitor
   **When** the overlay opens
   **Then** its restored size is constrained so the overlay remains usable on
   that monitor.

5. **Given** the overlay is ready and no request has started
   **When** the user selects the close button
   **Then** the overlay closes without sending an AI request.

6. **Given** an AI request is running
   **When** the user selects the close button
   **Then** the active request is canceled and the overlay closes with no answer
   result, matching **Esc** behavior.

7. **Given** an AI request completed successfully
   **When** the user selects the close button
   **Then** the overlay closes and the completed answer remains available to the
   existing save flow.

8. **Given** the overlay is resized or moved between monitors with different DPI
   settings
   **When** WPF processes the DPI transition
   **Then** the window does not compound its scale, jump back to the capture
   center after user interaction, or lose the remembered logical size.

9. **Given** the user continues to use **Enter**, **Esc**, title dragging, Skill
   selection, scrolling, or the opacity slider
   **When** the feature is present
   **Then** those interactions retain their current behavior.

## Constraints and assumptions

- Persisted dimensions are WPF device-independent pixels, not physical screen
  pixels.
- Only width and height are remembered; each new overlay remains centered over
  the selected region or monitor until the user moves it.
- The existing borderless, transparent, always-on-top appearance is retained.
- Resize affordances must not overlap or block the close button, title drag
  target, Skill selector, answer scrolling, or opacity slider.
- Closing continues to persist changed overlay preferences through the existing
  settings service.
- Tests that exercise settings normalization should remain independent of
  interactive WPF UI.

## Implementation outline

Add normalized overlay width and height properties to `AppSettings` alongside
the existing overlay opacity setting, and normalize them in `SettingsService`.
Restore logical dimensions before initial positioning and persist changed
dimensions when the overlay closes.

Update the frameless overlay to provide edge and corner resize hit targets while
retaining its custom appearance. Replace the title row with a layout that keeps
the drag target and a clearly labeled close button separate. Route the close
button and **Esc** through the same close operation so cancellation and completed
answer handling cannot diverge.

Adapt the existing DPI-aware positioning logic so initial centering uses the
restored dimensions, monitor transitions remain WPF-owned, and a user-initiated
resize is not overwritten by later recentering. Constrain restored dimensions to
the target monitor's usable area.

No ADR is required because this extends the existing overlay and settings
patterns without changing system structure, dependency direction, or a
difficult-to-reverse architectural choice.

## Tasks

- [x] Add persisted overlay width and height defaults, normalization, and
  settings round-trip tests.
- [x] Add DPI-safe edge and corner resizing with minimum and target-monitor
  bounds.
- [x] Make the overlay content respond correctly to resized dimensions.
- [x] Add the upper-right close button and share its close path with **Esc**.
- [x] Manually validate ready, running, and finished close behavior.
- [x] Manually validate resizing and restoration on same-DPI and mixed-DPI
  monitor configurations.
- [x] Update `FEATURES_AND_DEVELOPMENT.md`, `USER_MANUAL.md`, and
  `USER_MANUAL_KR.md`.
- [x] Run `.\scripts\verify.ps1`.

## Verification evidence

- 2026-09-12: `dotnet test .\src\SnipAgent.Tests\SnipAgent.Tests.csproj -c
  Debug --filter "FullyQualifiedName~SettingsServiceTests"` passed 23 tests with
  0 failures and compiled the updated WPF application.
- 2026-09-12: Final `.\scripts\verify.ps1` completed successfully: build passed
  with 0 warnings and 0 errors; all 44 tests passed with 0 failures and 0 skips.
- 2026-09-12: Manual validation passed with no defects for close-button behavior
  in ready, running, and finished states; all edge and corner resize handles;
  minimum-size enforcement and control usability; size restoration and initial
  recentering; and same-DPI and mixed-DPI monitor transitions.

## Related decisions

- None. This feature does not meet the ADR threshold.
