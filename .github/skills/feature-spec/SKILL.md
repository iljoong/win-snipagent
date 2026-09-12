---
name: feature-spec
description: Create, refine, review, or complete a lightweight SnipAgent feature specification. Use for medium, ambiguous, multi-slice, or multi-session product changes; when acceptance criteria or non-goals need clarification; or when implementation evidence must be reconciled with an existing spec. Do not use for small fixes or routine refactoring.
---

# Feature specification workflow

Use [the specification guide](../../../docs/specs/README.md) and
[the template](../../../docs/specs/template.md).

## 1. Decide whether a specification is warranted

Create or maintain a specification only when the change has multiple observable
behaviors, material edge cases or non-goals, several implementation slices, or
work that is likely to span sessions. For a small change, explain that the issue
or request is sufficient and do not create a document.

## 2. Gather verified context

Before drafting:

1. Read `AGENTS.md`.
2. Search `docs/specs/` for related work and update an existing specification
   rather than creating a duplicate.
3. Read relevant production code, tests, `docs/FEATURES_AND_DEVELOPMENT.md`, and
   user manuals.
4. Separate verified current behavior from assumptions.
5. Ask the user about decisions that materially affect behavior or scope. Never
   invent product requirements to fill a template.

## 3. Draft or update the specification

Use `docs/specs/<short-kebab-case-name>.md`. Keep the problem and desired outcome
implementation-independent. Include explicit in-scope and out-of-scope behavior,
observable Given/When/Then acceptance scenarios, constraints, and assumptions.

Keep the implementation outline concise. Break tasks into vertical slices that
can each be tested. Link an ADR when a significant technical decision is needed;
do not embed a full architecture decision in the specification.

Use these statuses:

- `Draft`: important requirements remain unresolved.
- `Approved`: scope and acceptance scenarios are ready for implementation.
- `Implementing`: work is in progress.
- `Done`: acceptance scenarios and documentation reconciliation are complete.

Do not mark a document `Approved` or `Done` while material uncertainty remains.

## 4. Reconcile after implementation

Read the implementation and test results rather than assuming the original plan
was followed. Update the task list, document any approved scope changes, and
record exact verification commands, results, and manual Windows validation.

Update `docs/FEATURES_AND_DEVELOPMENT.md` for changed current behavior and both
user manuals for user-visible changes. Do not duplicate those documents inside
the specification.

## Quality check

Confirm that:

- each acceptance scenario is observable and testable;
- non-goals prevent likely scope expansion;
- assumptions are labeled rather than stated as facts;
- tasks describe outcomes, not just architectural layers;
- verification evidence is specific;
- links are relative and valid;
- dates use `YYYY-MM-DD`.
