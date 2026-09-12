# Copilot cloud execution

This guide defines how SnipAgent work moves between local development and GitHub
Copilot cloud agent. The cloud agent is an implementation resource, not a
requirements approver or merge authority.

## Operating model

```text
Draft or issue refinement
        ↓
Human approval
        ↓
Cloud dispatch
        ↓
Cloud-owned task branch and draft PR
        ↓
Cloud review iterations and Ubuntu compilation
        ↓
Local Windows automated and manual validation
        ↓
Final evidence and Done status in the PR
        ↓
Human approval and merge
```

Specification status and execution location are independent. Continue using
`Draft`, `Approved`, `Implementing`, and `Done`; do not add cloud-specific
statuses.

## Prerequisites

Before dispatch:

1. Use an `Approved` feature specification, or a precise small issue that does
   not need a specification.
2. Resolve product and architecture decisions. Link an accepted or proposed ADR
   when required.
3. Ensure the work order and referenced files are committed and available to
   the cloud agent from GitHub.
4. Confirm that no developer or other agent owns the intended task branch.
5. Invoke the `cloud-execution` skill to check readiness and prepare the
   assignment.

The repository-wide instructions are in `AGENTS.md`. GitHub discovers them
through `.github/copilot-instructions.md`.

## Starting a cloud session

Start from the GitHub Agents page, assign a suitable issue to Copilot, or use a
supported IDE cloud-agent entry point. Prefer an existing issue because it
provides a stable work order and discussion history.

Use this assignment shape:

```text
Implement [issue or approved specification link].

Outcome:
[Observable outcome and reason.]

In scope:
- [...]

Out of scope:
- [...]

Acceptance:
- [...]

Execution rules:
- Follow AGENTS.md and linked specifications and ADRs.
- Set the approved specification to Implementing before application edits.
- Work on one task branch and open a draft pull request.
- You may commit and push only to the task branch.
- Run bash ./scripts/verify-ubuntu.sh and record exact results as Ubuntu
  compile-only evidence.
- Leave Windows tests and interactive checks pending.
- Keep the specification Implementing until all required evidence exists.
- Do not push to main, approve, merge, or claim unperformed validation.

Handoff:
- Summarize changes and unresolved risks.
- List exact commands, results, and environment.
- List Windows automated and manual checks still required.
```

Starting a session, dispatching implementation, and completing the task are
different events. Do not report that cloud execution started unless the GitHub
session or assignment was actually created.

## Ubuntu environment

Copilot cloud agent uses Ubuntu by default. Its setup is defined in
`.github/workflows/copilot-setup-steps.yml`, which installs .NET 10 and runs:

```bash
bash ./scripts/verify-ubuntu.sh
```

The script:

- requires the .NET 10 SDK;
- restores and compiles the Debug `SnipAgent.slnx` solution with Windows
  targeting enabled;
- isolates Ubuntu outputs from any Windows-generated `bin` and `obj` files;
- exits nonzero on restore or compilation failure;
- explicitly reports that tests were not run.

This evidence proves only that the Windows-targeted projects restore and
compile from Ubuntu. Ubuntu does not provide `Microsoft.WindowsDesktop.App` and
cannot validate WPF, WinForms, WinRT, Win32, capture, clipboard, tray, DPI,
multi-monitor, or other interactive Windows behavior.

Do not add success-shaped fallbacks when Ubuntu compilation fails. Record and
fix the failure or identify it as a blocker.

## Draft PR handoff

The cloud agent opens a draft PR while the specification is `Implementing`.
Use the repository pull-request template and divide evidence into:

### Cloud or Ubuntu

- `bash ./scripts/verify-ubuntu.sh`
- exact restore and compilation result
- environment and SDK version
- limitations or skipped checks

### Windows automated

- focused `dotnet test` commands
- final `.\scripts\verify.ps1`
- test counts, failures, warnings, and errors

### Manual Windows

- each required interactive acceptance scenario
- tested Windows and monitor/DPI configuration when relevant
- pass, fail, or not-run result

Cloud evidence cannot satisfy the Windows headings.

## Review and iteration

While Copilot owns the task branch:

1. Review the complete diff and draft PR.
2. Return concrete findings through the same cloud session or PR.
3. Let the cloud agent update its branch and evidence.
4. Avoid concurrent local edits to that branch.

If local implementation becomes necessary, explicitly transfer ownership,
record the handoff in the PR, and stop asking the cloud agent to edit that
branch until ownership is returned.

## Windows validation and completion

On a Windows machine:

1. Check out the reviewed cloud branch without overwriting unrelated local
   changes.
2. Run focused tests for the changed behavior.
3. Run `.\scripts\verify.ps1`.
4. Perform the manual scenarios required by the specification.
5. Record exact results in the specification and PR.

Only after all acceptance scenarios, documentation, ADR requirements, automated
checks, and manual validation are complete may the final PR revision change the
specification from `Implementing` to `Done`.

A human reviews and merges the PR. Copilot cloud must not approve or merge its
own work.

## Failure and cancellation

- If setup or Ubuntu compilation fails, keep the work in progress and record
  the exact failure.
- If Windows validation fails, return the defect to the current branch owner.
- If scope changes materially, pause implementation and revise or reapprove the
  specification first.
- If work is canceled, close the cloud session or PR and reconcile the
  specification without erasing useful decision or verification history.
