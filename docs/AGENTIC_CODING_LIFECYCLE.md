## Lightweight agentic coding lifecycle

The normal lifecycle is:

```text
Idea
  ↓
Draft
  ↓
Approved
  ↓
Implementing
  ↓
Done
```

Execution location is a separate concern from specification status:

```text
Approved
  ↓
Dispatch
  ├─ Local implementation
  └─ Copilot cloud implementation → task branch → draft PR
                                      ↓
                                local Windows handoff
  ↓
Done when all required evidence exists
  ↓
Human approval and merge
```

Do not add `Cloud`, `Delegated`, or `PR Open` as specification statuses. They
describe execution and review state, not product maturity.

Not every change needs this process. Small fixes can go directly from request to implementation.

```text
Small fix:
Request → Implement → Verify → Commit

Medium feature:
Draft → Approved → Implementing → Verify → Done → Commit/PR

Architectural change:
Draft → ADR proposed/accepted → Implementing → Verify → Done → Commit/PR

Cloud execution:
Approved spec or precise issue → Cloud branch → Draft PR → Review/iterate
                               → Windows validation → Done in PR → Merge
```

The examples below describe how you would start that feature again or handle a future feature.

---

# 1. Idea and classification

Start by describing the desired outcome. Ask the agent to determine whether a persistent specification is warranted.

### Example prompt

```text
I want users to resize the AI answer overlay and close it with a visible button.

Determine whether this needs a feature specification. Inspect the current
implementation and documentation, but do not modify application code yet. Follow
AGENTS.md and use the feature-spec skill when appropriate.
```

The agent should:

1. Read relevant code and documentation.
2. Determine the size and ambiguity of the change.
3. Reuse an existing related spec if one exists.
4. Ask about material product decisions.
5. Either create a Draft spec or explain why one is unnecessary.

### When no spec is needed

For example:

```text
Fix the typo in the AI answer overlay title. This is a small change; do not
create a feature specification or ADR. Update the code, run the smallest
relevant validation, and show me the result.
```

---

# 2. Draft

A specification is **Draft** while requirements or important behavioral choices remain unresolved.

Typical unresolved questions might include:

- Should the resized window dimensions be remembered?
- What should the minimum size be?
- Should closing during an AI request cancel it?
- Should the window position also be remembered?
- What happens on a smaller monitor?

A Draft is for deciding **what should happen**, not yet implementing it.

### Prompt to create a Draft

```text
/feature-spec

Create a Draft feature specification for making the AI answer overlay resizable
and adding a close button.

Inspect the current overlay, settings persistence, DPI positioning, tests, and
user manuals. Separate verified current behavior from assumptions. Ask me about
decisions that materially affect behavior. Do not implement the feature yet.
```

### Prompt to review a Draft

```text
Review #file:docs/specs/resizable-ai-answer-overlay.md.

Find ambiguous requirements, missing edge cases, untestable acceptance criteria,
unnecessary scope, and assumptions presented as facts. Ask me one product
decision at a time. Do not modify application code.
```

### Prompt to refine it

```text
Refine #file:docs/specs/resizable-ai-answer-overlay.md using these decisions:

- Remember width and height between captures.
- Do not remember the previous screen position.
- The close button behaves exactly like Esc.
- Minimum size is 480 x 320 DIPs.
- Resizing applies only to the AI answer overlay.

Keep the status Draft if any material decision remains unresolved. Otherwise,
tell me whether it is ready for approval.
```

## Draft exit criteria

Move out of Draft when:

- The problem is clear.
- In-scope and out-of-scope behavior is explicit.
- Material product decisions are resolved.
- Acceptance scenarios are observable.
- Assumptions and constraints are documented.
- The implementation is small enough to plan.

---

# 3. Approved

**Approved** means the specification is ready to implement.

Approval is a user decision. An agent may recommend approval, but it should not silently decide that an ambiguous requirement is acceptable.

### Approval prompt

```text
Review #file:docs/specs/resizable-ai-answer-overlay.md for implementation
readiness.

Confirm that scope, non-goals, acceptance scenarios, constraints, and required
validation are complete. If no material ambiguity remains, change the status
from Draft to Approved. Do not implement it yet.
```

A more direct approval prompt is:

```text
I approve #file:docs/specs/resizable-ai-answer-overlay.md.

Change its status to Approved and update the Last reviewed date. Do not modify
application code.
```

## Approved does not mean started

At this point:

- Requirements are stable.
- Implementation has not necessarily begun.
- Task checkboxes should generally remain unchecked.
- Verification evidence should say that implementation has not started.

## Approved exit criteria

Move to Implementing only when someone begins changing code for the feature.

---

# 4. Implementing

**Implementing** means application work has started.

The first implementation step should change the status from `Approved` to `Implementing`.

### Standard implementation prompt

```text
Implement #file:docs/specs/resizable-ai-answer-overlay.md.

Follow AGENTS.md and the approved specification.

Before changing application code:
1. Confirm the spec is Approved.
2. Change its status to Implementing.
3. Inspect the relevant implementation, tests, and documentation.

Then implement the work in independently verifiable vertical slices. Add or
update tests, keep the task checklist accurate, update affected documentation,
and run scripts\verify.ps1.

Do not mark the specification Done until every required automated and manual
acceptance scenario has been verified. When working locally, do not commit or
push without my approval. Copilot cloud may commit and push only to its task
branch under the cloud execution workflow below.
```

### More controlled, slice-by-slice prompt

```text
Begin implementing #file:docs/specs/resizable-ai-answer-overlay.md.

For this turn, implement only the persisted overlay width and height:
- Add defaults and normalization.
- Add settings round-trip tests.
- Update the spec status to Implementing.
- Check only the task completed in this turn.
- Run the focused tests.

Do not implement resizing or the close button yet.
```

Then continue:

```text
Continue #file:docs/specs/resizable-ai-answer-overlay.md.

Implement the resize behavior described in the next unchecked task. Preserve
the current DPI and positioning behavior. Run focused validation and update the
task checklist, but do not mark the full spec Done.
```

And:

```text
Continue #file:docs/specs/resizable-ai-answer-overlay.md.

Implement the close button and route it through the same close/cancellation
behavior as Esc. Validate ready, running, and finished state logic where it can
be automated. Update the task checklist and verification evidence.
```

## During implementation

The agent should continuously reconcile the spec:

- Check completed tasks.
- Leave uncompleted tasks unchecked.
- Record approved scope changes.
- Record actual test commands and results.
- Update acceptance criteria only when behavior was intentionally changed.
- Avoid changing the spec afterward merely to make incorrect code appear compliant.

## Handling a requirement change

If you change your mind during implementation:

```text
Pause implementation of
#file:docs/specs/resizable-ai-answer-overlay.md.

Change the requirement so overlay size is no longer persisted. Identify how this
affects acceptance scenarios, tasks, implementation already completed, tests,
and documentation. Update the spec first and show me the revised scope before
changing more application code.
```

## Handling a blocker

```text
Record the current implementation blocker in
#file:docs/specs/resizable-ai-answer-overlay.md.

Explain which acceptance scenario is blocked, what evidence revealed the
problem, and what decisions or work remain. Keep the status Implementing. Do not
claim the blocked task is complete.
```

---

# 5. Cloud execution

Use cloud execution for bounded work that can be reviewed through a task branch
and draft pull request. Keep product decisions and final merge authority with
the user. See [Cloud execution](./CLOUD_EXECUTION.md) for the complete operating
guide and use the repository's `cloud-execution` skill to prepare or start a
delegation.

## Dispatch gate

Delegate only when one of these inputs is ready:

- a feature specification with status `Approved`; or
- a small issue with explicit scope, non-goals, acceptance scenarios, and
  validation expectations.

The approved specification remains the requirements source of truth. The issue
or cloud prompt is the work order and must link the specification. Ensure the
input exists on the remote default branch or in a GitHub issue; an unpushed
local document is invisible to the cloud agent.

## Branch ownership

- One agent owns a task branch at a time.
- Copilot cloud may create commits, push its task branch, and open or update a
  draft pull request.
- Copilot cloud must not push to `main`, approve its own work, or merge.
- Do not edit the cloud task branch locally while the cloud agent owns it.
- Send review findings and failed checks back through the same cloud session or
  pull request. Explicitly transfer ownership before making substantial local
  corrections.

## Cloud implementation prompt

```text
/cloud-execution

Prepare and start cloud execution for
#file:docs/specs/resizable-ai-answer-overlay.md.

Use an existing GitHub issue if one already tracks this work. Otherwise prepare
a concise issue or cloud-session prompt. Require a task branch and draft pull
request. The cloud agent may commit and push only to that branch.

Require it to:
- change the approved specification to Implementing before application edits;
- work only within approved scope;
- implement independently verifiable vertical slices;
- add or update focused tests;
- run bash ./scripts/verify-ubuntu.sh;
- label all evidence with the environment where it was collected;
- leave Windows tests and interactive checks pending;
- keep the specification Implementing until all required evidence is supplied.

Do not allow direct pushes to main, self-approval, merge, or claims of
unperformed Windows validation.
```

## Cloud handoff

The cloud agent should open a draft PR while the specification remains
`Implementing`. Its handoff must include:

- the issue and specification links;
- what changed and why;
- exact cloud or Ubuntu commands and results;
- tests not run and why;
- required Windows automated and manual checks;
- known risks, limitations, and unresolved review items.

Ubuntu is a compile-only environment for this Windows application. The command
`bash ./scripts/verify-ubuntu.sh` restores and builds the solution but does not
run its Windows-targeted tests or validate WPF, WinForms, WinRT, Win32, DPI,
capture, clipboard, or interactive behavior.

## Review and completion

Review the draft PR, then run focused tests and `.\scripts\verify.ps1` on
Windows. Perform every required interactive scenario. Send defects back to the
same cloud session while it owns the branch.

The final PR revision may change the specification to `Done` only after all
cloud, Windows automated, and manual evidence is recorded. That status becomes
authoritative when the PR is merged by a human.

---

# 6. Automated verification

After implementation, run focused validation first and full validation second.

### Prompt

```text
Verify the implementation of
#file:docs/specs/resizable-ai-answer-overlay.md.

Run the smallest relevant automated tests first, then scripts\verify.ps1.
Inspect the complete diff for regressions, stale documentation, unrelated
changes, and unmet acceptance scenarios.

Record exact commands, test counts, failures, warnings, and limitations in the
specification. Label each result with its environment. Do not mark manual
validation as complete.
```

Good verification evidence looks like:

```markdown
- 2026-09-12: `dotnet test ... --filter SettingsServiceTests` passed
  23 tests with 0 failures on Windows.
- 2026-09-12: `bash ./scripts/verify-ubuntu.sh` completed successfully on
  Ubuntu. Restore and compilation passed; tests were not run.
- 2026-09-12: `.\scripts\verify.ps1` completed successfully on Windows. Build passed
  with 0 warnings and 0 errors; all 44 tests passed.
```

Bad evidence looks like:

```markdown
- Tests look good.
- Verified successfully.
- The agent confirmed the feature works.
```

Evidence should be reproducible and specific.

---

# 7. Manual validation

Some WPF behavior cannot be meaningfully verified through unit tests:

- Mouse resizing.
- Resize cursors.
- Minimum size usability.
- Scrolling after resizing.
- Closing while a real network request is active.
- Multi-monitor DPI transitions.
- Visual overlap and clipping.

The user normally performs these checks.

### Prompt before testing

```text
Read #file:docs/specs/resizable-ai-answer-overlay.md and produce a concise manual
test checklist for only the acceptance scenarios that cannot be verified
automatically.

Do not change the spec or claim any test passed. Wait for my results.
```

### Prompt after testing

```text
Update #file:docs/specs/resizable-ai-answer-overlay.md with these manual results:

- Close button in Ready state: passed; no request started.
- Close button in Running state: passed; request canceled.
- Close button in Finished state: passed; answer was preserved for saving.
- All four edges and four corners: passed.
- Minimum 480 x 320 size: passed.
- Scrolling and controls at minimum size: passed.
- Reopen restored size and recentered position: passed.
- Tested on one 100% DPI monitor only.
- Mixed-DPI validation was not performed.

Check only scenarios supported by this evidence. Keep mixed-DPI validation
unchecked and keep the status Implementing.
```

That final sentence is important: **partial manual validation should not produce a false Done status**.

---

# 8. Done

A spec becomes **Done** when:

- All in-scope behavior is implemented.
- Acceptance scenarios pass.
- Automated verification passes.
- Required manual validation is complete.
- Current development documentation is updated.
- User manuals are updated for user-visible behavior.
- ADR requirements are satisfied, if applicable.
- Verification evidence is recorded.
- No unresolved blocker remains.

### Closure prompt

```text
Close the implementation for
#file:docs/specs/resizable-ai-answer-overlay.md.

Review the specification, implementation, tests, current documentation, and
recorded manual results. Check only tasks supported by evidence.

If every acceptance scenario and required validation is complete:
- Change the status from Implementing to Done.
- Update Last reviewed.
- Replace pending verification notes with final evidence.
- Confirm that no ADR is required, or link the relevant ADR.
- Run scripts\verify.ps1 one final time.
- Show me the final diff.

If anything remains incomplete, keep the status Implementing and list exactly
what remains. Do not commit or push.
```

### Closure prompt with evidence included

```text
Close #file:docs/specs/resizable-ai-answer-overlay.md.

Manual validation completed:
- Ready close: passed.
- Running close/cancellation: passed.
- Finished close and answer preservation: passed.
- Edge and corner resizing: passed.
- Minimum size and control usability: passed.
- Size restoration and initial recentering: passed.
- Mixed-DPI transition between 100% and 150% monitors: passed.
- No defects or limitations observed.

Review the implementation against every acceptance scenario, run
scripts\verify.ps1, record the final evidence, check completed tasks, and mark
the specification Done only if everything is supported. Do not commit yet.
```

---

# 9. Commit, PR, merge, and push

For local execution, commit the implementation and closure together when
possible after the spec reaches Done. For cloud execution, commits and a draft
PR are expected during `Implementing`; the final PR revision records complete
evidence and changes the spec to Done before human approval and merge.

### Commit prompt

```text
Prepare the completed feature for commit.

Confirm the related specification is Done, scripts\verify.ps1 passes, the
worktree contains only related changes, and documentation is reconciled. Then
commit the implementation with a concise message. Do not push.
```

### PR prompt

```text
Prepare a pull request summary for this completed feature.

Use the repository PR template. Include:
- What changed and why.
- The feature-spec link.
- Whether an ADR was required.
- Exact verification commands and results.
- Manual validation performed.
- Remaining risks or limitations.

Do not claim independent review or unperformed validation.
```

### Merge prompt

```text
Merge the completed feature branch into main.

Before merging, confirm:
- The feature specification is Done.
- The worktree is clean.
- The branch contains only intended commits.
- scripts\verify.ps1 passed.
- The target branch is correct.

Use a non-interactive merge, report the resulting commit, and do not push unless
I explicitly request it.
```

Copilot cloud must not perform this merge. Cloud task branches and draft PRs are
implementation artifacts, not approval.

### Push prompt

```text
Push local main to origin/main.

First confirm the worktree is clean and show which commits are ahead of
origin/main. Then push and report the remote result.
```

---

# 10. Rejected, canceled, or deferred work

`Draft`, `Approved`, `Implementing`, and `Done` are sufficient for the normal path, but sometimes work stops.

For this lightweight workflow, avoid adding many permanent statuses. Record a clear note near the top instead.

### Cancel a Draft

```text
Cancel #file:docs/specs/example-feature.md.

Add a short note explaining that the feature was considered but will not be
implemented. Preserve useful product reasoning, remove transient implementation
tasks, and move it to docs/specs/archive/ if that directory exists. Do not
delete decision history without asking me.
```

### Defer an Approved spec

```text
Defer #file:docs/specs/example-feature.md.

Keep its status Approved, add a dated note explaining why implementation is
deferred, and ensure no task is presented as actively in progress.
```

### Revert an implemented feature

A code revert and a spec closure are separate concerns. After reverting:

```text
Reconcile #file:docs/specs/example-feature.md after the implementation was
reverted.

Inspect the revert commit. Do not leave the spec marked Done if the behavior no
longer exists. Record that implementation was reverted, why it was reverted,
and whether the feature may be reconsidered. Preserve prior verification
history rather than rewriting it.
```

---

# Recommended everyday prompt set

These six prompts cover most solo development work.

### Create

```text
/feature-spec

Create a lightweight spec for: [feature description].
Inspect current behavior first, ask about material ambiguities, and do not
implement yet.
```

### Approve

```text
Review #file:[spec path] for readiness. If no material ambiguity remains, mark
it Approved. Do not implement.
```

### Implement

```text
Implement #file:[spec path]. Follow AGENTS.md, set it to Implementing, work in
vertical slices, add tests, reconcile documentation, and do not mark it Done
without complete evidence.
```

### Delegate to cloud

```text
/cloud-execution

Prepare and start cloud execution for #file:[approved spec path or precise issue].
Require a task branch and draft PR, run Ubuntu compilation, preserve pending
Windows validation, and keep merge authority with me.
```

### Close

```text
Close #file:[spec path]. Verify every acceptance scenario, ask me for missing
manual results, run scripts\verify.ps1, and mark it Done only when all required
evidence exists.
```

### Commit and merge

```text
Commit and merge the completed feature into main. Confirm the spec is Done,
verification passes, and the worktree contains only related changes. Do not
push without my approval.
```

The key discipline is simple:

> **The agent may write code, but status transitions must reflect evidence—not confidence.**