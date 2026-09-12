---
name: architecture-decision
description: Create, review, accept, or supersede a SnipAgent architecture decision record. Use when a choice changes system structure or dependencies, affects security/reliability/performance/operability, introduces a significant platform or dependency, is expensive to reverse, or is likely to be debated again. Do not use for routine implementation choices.
---

# Architecture decision workflow

Use [the ADR guide](../../../docs/adr/README.md) and
[the template](../../../docs/adr/template.md).

## 1. Apply the ADR threshold

State whether the decision meets at least one threshold from the ADR guide. If it
does not, keep the rationale in the feature specification, issue, code, or pull
request as appropriate and do not create an ADR.

## 2. Gather evidence

Before writing:

1. Read `AGENTS.md`, the related feature specification, relevant code and tests,
   and current architecture documentation.
2. Search `docs/adr/` and `docs/PLAN.md` for related decisions.
3. Distinguish verified historical context from inferred rationale.
4. Identify decision drivers and at least two viable options when they exist.
5. Ask the user when the choice or trade-off has not actually been decided.

Never invent retrospective intent or present an agent's preference as an accepted
decision.

## 3. Create a record

Find the highest existing four-digit ADR number and allocate the next number,
starting at `0001`. Use `docs/adr/NNNN-short-kebab-case-title.md` and preserve
the template headings.

Keep the record pithy and factual. Describe meaningful advantages,
disadvantages, operational impact, and follow-up work. Leave the status
`Proposed` until explicit acceptance or until the implementing change establishes
acceptance under the repository workflow.

Add the ADR to the decision index in `docs/adr/README.md`.

## 4. Supersede, do not rewrite

When changing an accepted decision:

1. Create a new ADR.
2. Set its `Supersedes` field to the old ADR.
3. Set the old ADR's `Superseded by` field to the new ADR.
4. Change only the old ADR's status and supersession metadata; preserve its
   original context, options, decision, and consequences.
5. Update the decision index.

## Quality check

Confirm that:

- the ADR threshold is explicitly satisfied;
- context explains why a decision is needed now;
- options and trade-offs are fair and concrete;
- the decision is assertive and linked to its drivers;
- positive and negative consequences are recorded;
- validation or reconsideration evidence is defined;
- status and supersession links are consistent;
- dates use `YYYY-MM-DD`.
