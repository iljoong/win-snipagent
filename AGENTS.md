# SnipAgent Agent Guide

This file is the canonical guide for coding agents working in this repository.
Keep it short and link to deeper documentation rather than duplicating it.

## Project

SnipAgent is a Windows-only screenshot utility built with C# 14, .NET 10, WPF,
WinForms interoperability, WinRT OCR, and Win32 APIs. It runs as a tray
application and supports region or monitor capture, OCR, OpenAI-compatible AI
capture, and user-defined AI Skills.

## Repository map

- `src/SnipAgent.App/`: production WPF application.
- `src/SnipAgent.Tests/`: xUnit tests for logic that does not require interactive
  UI or platform interoperability.
- `docs/FEATURES_AND_DEVELOPMENT.md`: canonical current behavior, architecture
  summary, project layout, and developer guidance.
- `docs/PLAN.md`: historical implementation and design record; do not use it as
  the task backlog.
- `docs/USER_MANUAL.md` and `docs/USER_MANUAL_KR.md`: user-facing documentation.
- `docs/specs/`: specifications for medium or multi-session changes.
- `docs/adr/`: append-only records of architecturally significant decisions.

## Commands

The .NET 10 SDK is required.

```powershell
# Build and run all automated tests.
.\scripts\verify.ps1

# Compile the Windows-targeted solution from Ubuntu. This does not run tests.
bash ./scripts/verify-ubuntu.sh

# Run the application on Windows.
dotnet run --project .\src\SnipAgent.App\SnipAgent.App.csproj

# Publish a Windows build.
dotnet publish .\src\SnipAgent.App\SnipAgent.App.csproj -c Release -o .\Out
```

The solution can compile on non-Windows systems because Windows targeting is
enabled. Running the application or tests requires Windows and the
`Microsoft.WindowsDesktop.App` runtime.

## Architectural constraints

- Keep screen capture, monitor bounds, cropping, and native window positioning in
  physical pixels. Convert to WPF device-independent pixels only at UI boundaries.
- Preserve the freeze-before-selection capture model so overlays never appear in
  captured output.
- Preserve Per-Monitor-V2 DPI behavior and support negative virtual-desktop
  coordinates.
- Keep clipboard operations and other COM/Windows UI work on an appropriate STA
  thread.
- Store API keys only in Windows Credential Manager. Never write secrets to
  settings, logs, fixtures, documentation, or source control.
- Extraction or AI failures must be surfaced without discarding an otherwise
  valid screenshot.
- Keep cancellation, native handles, bitmaps, clients, and windows explicitly
  disposed.

## Change workflow

1. Read the relevant code, tests, and canonical documentation before editing.
2. For small changes, work directly from the issue or request.
3. For medium, ambiguous, or multi-session features, create or update a file in
   `docs/specs/` before implementation. Use the `feature-spec` skill.
4. Create an ADR only for a structurally significant, difficult-to-reverse, or
   cross-cutting decision. Use the `architecture-decision` skill.
5. Before delegating work to Copilot cloud agent, use the `cloud-execution`
   skill and follow `docs/CLOUD_EXECUTION.md`.
6. Implement in small, independently verifiable vertical slices.
7. Add or update focused tests for changed behavior.
8. Run the smallest relevant tests while iterating, then run
   `.\scripts\verify.ps1` before completion.
9. Inspect the complete diff and reconcile affected documentation.

Do not create permanent plan files for work that fits in one session. Do not use
`docs/PLAN.md` as a live checklist. Do not rewrite accepted ADRs; supersede them
with a new record.

## Definition of done

- The requested behavior and explicit non-goals are satisfied.
- Changed behavior has appropriate automated tests or documented manual validation.
- `.\scripts\verify.ps1` passes on Windows.
- User-visible changes update both user manuals when applicable.
- Current behavior or developer workflow changes update
  `docs/FEATURES_AND_DEVELOPMENT.md`.
- The related feature spec records verification evidence, when a spec exists.
- Significant decisions are recorded or superseded in `docs/adr/`.
- No generated output, credentials, local settings, or unrelated changes are
  included.

Ubuntu compilation with `bash ./scripts/verify-ubuntu.sh` is useful cloud-agent
evidence, but it does not replace Windows tests or manual Windows validation.

## Sensitive changes

Review changes to credential handling, capture and coordinate calculations,
threading, native resource lifetime, MCP endpoint validation, package references,
application manifests, and `.github/workflows/` especially carefully. Do not
install dependencies, weaken validation, or change release/deployment behavior
without an explicit requirement.
