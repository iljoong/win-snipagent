---
name: cloud-execution
description: Prepare, initiate, or continue SnipAgent work with GitHub Copilot cloud agent. Use when delegating an approved feature specification or precise small issue to the cloud, preparing the cloud assignment, starting a cloud session through an available GitHub integration, or managing the draft-PR handoff. Do not use for local-only implementation or for unresolved product design.
---

# Copilot cloud execution workflow

Follow [the cloud execution guide](../../../docs/CLOUD_EXECUTION.md) and
[the coding lifecycle](../../../docs/AGENTIC_CODING_LIFECYCLE.md).

## 1. Establish the work order

Identify exactly one source:

- an `Approved` feature specification; or
- a small GitHub issue or request with explicit scope, non-goals, observable
  acceptance scenarios, and validation expectations.

Do not delegate a Draft specification or invent missing product decisions. Ask
the user to resolve material ambiguity first. Reuse an existing issue when one
already tracks the work.

Confirm that every file the cloud agent needs exists on the remote default
branch. Local uncommitted or unpushed context is not available to it.

## 2. Check delegation readiness

Read `AGENTS.md`, the work order, relevant specifications and ADRs, and the
pull-request template. Confirm:

- the task is bounded enough for one task branch and reviewable draft PR;
- acceptance scenarios distinguish automated from manual Windows validation;
- dependencies and required tools are available without repository secrets;
- no other agent or developer owns the intended task branch;
- sensitive work has explicit review expectations.

Do not change an approved specification merely to make delegation easier.

## 3. Prepare the assignment

Produce a concise issue body or cloud-session prompt containing:

1. the outcome and reason;
2. links to the issue, approved specification, and relevant ADRs;
3. in-scope and out-of-scope behavior;
4. acceptance scenarios;
5. required implementation and documentation work;
6. exact cloud validation command: `bash ./scripts/verify-ubuntu.sh`;
7. tests and manual checks that must remain pending for Windows;
8. branch, draft-PR, evidence, and handoff rules.

Require the cloud agent to set an approved specification to `Implementing`
before application edits. Allow commits and pushes only to its task branch.
Require a draft PR and forbid direct pushes to `main`, self-approval, and merge.

## 4. Start or hand off

If an authenticated GitHub cloud-agent integration is available, show the final
assignment and obtain user confirmation before creating an issue, assigning
Copilot, or starting a session. Use the existing issue when possible.

If no launch integration is available, do not claim that execution started.
Return the ready-to-paste assignment and identify an appropriate GitHub entry
point: the Agents page, an issue assigned to Copilot, or the IDE cloud-agent
command.

## 5. Continue through the draft PR

Keep one owner for the cloud task branch. Send review findings and failed checks
back through the same cloud session or PR. Do not make substantial local changes
on that branch until ownership is explicitly transferred.

Require evidence under three headings:

- `Cloud or Ubuntu`: exact compile-only commands and results;
- `Windows automated`: focused tests and `.\scripts\verify.ps1`;
- `Manual Windows`: interactive acceptance scenarios.

Ubuntu success never satisfies either Windows category. Keep the specification
`Implementing` while any required evidence or review work remains.

## Quality check

Confirm that:

- the work order is remotely accessible and unambiguous;
- the cloud agent has one bounded task and one owned branch;
- the assignment requires a draft PR and environment-labeled evidence;
- Windows tests and manual scenarios are not delegated to Ubuntu;
- no secret, credential, or broad permission is requested;
- only a human can approve and merge;
- starting, dispatching, and completing are reported as distinct events.
