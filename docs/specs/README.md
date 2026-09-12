# Feature specifications

Feature specifications capture the intent and acceptance criteria for medium,
ambiguous, or multi-session changes. They are not required for small fixes,
routine dependency updates, or straightforward refactoring.

## When to create a specification

Create one when a change has multiple user-visible behaviors, meaningful
non-goals, unresolved edge cases, several implementation slices, or work that
will span sessions. Use the repository's `feature-spec` Agent Skill and start
from [template.md](./template.md).

## Lifecycle

1. Create `docs/specs/<short-feature-name>.md` with status `Draft`.
2. Resolve material ambiguities and make acceptance scenarios observable.
3. Change the status to `Approved` before implementation.
4. Keep the task checklist and implementation outline current while working.
5. Record commands, results, and required manual validation.
6. Set the status to `Done` only after acceptance scenarios are verified and
   affected canonical documentation is reconciled.

Specifications describe intended behavior and constraints. Current released
behavior remains documented in
[FEATURES_AND_DEVELOPMENT.md](../FEATURES_AND_DEVELOPMENT.md) and the user
manuals. Do not generate a specification from code and treat inferred intent as
fact.

Completed specifications remain as concise design history. Remove obsolete task
details that no longer help explain the delivered change, but preserve the
problem, scope, acceptance scenarios, and verification evidence.
