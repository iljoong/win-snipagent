# Architecture decision records

This directory is the append-only log of architecturally significant decisions
for SnipAgent. Start from [template.md](./template.md).

## ADR threshold

Create an ADR when a decision changes system structure or dependency direction,
affects a key quality attribute such as security or reliability, introduces a
significant platform or dependency, is expensive to reverse, or is likely to be
revisited. Do not create ADRs for ordinary refactoring, local UI choices, routine
package updates, or individual bug fixes.

## Workflow

1. Search this directory for an existing related decision.
2. Copy the template to the next four-digit sequence number:
   `NNNN-short-decision-title.md`.
3. Record real context, decision drivers, considered options, and consequences.
4. Keep the status `Proposed` until the decision is explicitly accepted.
5. Accept the ADR in the same change that implements the decision when practical.
6. If an accepted decision changes, create a new ADR and link both records with
   `Supersedes` and `Superseded by`. Never rewrite accepted history to make it
   appear current.

Use the repository's `architecture-decision` Agent Skill to create, review, or
supersede a record.

## Decision index

No architecture decision records have been accepted in this directory yet.
Existing choices described in [PLAN.md](../PLAN.md) are historical context; do
not manufacture retrospective rationale for them.
