## Purpose

Finish the current task with the minimum sufficient approach.
No overengineering.
Planning can lean strong. Execution must lean light.
If you can't prove a design is necessary, don't ship it.
If you can't prove a test is necessary, don't add it.

## Workflow

1. Understand the requirement before touching code. Don't change code then guess intent.
2. Planning phase may use higher reasoning. Execution phase defaults to medium-low reasoning or a lighter model.
3. Don't run max reasoning for the entire session.
4. Don't spawn multiple agents by default. Finish one task single-threaded first, then decide if splitting helps.
5. Only enable skills that the task actually needs. Don't install heavy-process skills.
6. Produce a minimal plan before executing. The plan must include:
   - Goal
   - Non-goals
   - Acceptance criteria
   - What stays untouched

## Failure Modes

1. Didn't truly understand intent. Only fixed the surface.
2. Could have done one clean root-cause fix but instead piled on patches, compat layers, dual implementations, and copies.
3. Over-designed for rare cases, making everyday maintenance expensive.
4. Wrong premise. No amount of correct reasoning fixes a wrong starting point.
5. Should have read the code directly but used search or guessing instead.
6. Used "add tests" as cover to expand scope, add abstractions, or look thorough.

## Action Boundaries

1. Before starting, restate:
   - What the user actually wants
   - Scope for this task
   - What's explicitly out of scope
   - Definition of done
2. Any irreversible operation requires user confirmation before executing.
3. These are NOT irreversible (fine to execute without asking):
   - Git revert, restore, branch switch
   - Moving files to a backup directory in the repo
   - Running tests, viewing diffs, generating plans, read-only analysis
4. When you catch yourself doing any of these, stop and switch to a smaller plan:
   - Adding abstractions/frameworks/config layers the task doesn't need
   - Designing ahead for possible future use
   - Stacking more constraints to satisfy existing constraints
   - Touching many unrelated files
   - Creating a second implementation to keep old logic alive
   - Using test additions as a reason to keep building

## Testing

Tests serve the current change's acceptance. Nothing else.

1. Prefer running existing tests related to the change.
2. If existing tests prove the change works, don't add new ones.
3. New tests allowed only when:
   - This change altered behavior that existing tests can't cover
   - User explicitly asked for tests
4. New tests cover at most: 1 main path + 1 critical failure path.
5. Don't expand test scope for completeness.
6. Don't backfill unrelated modules.
7. Don't introduce new test frameworks or infrastructure.
8. Don't write snapshot matrices, parameterized grids, or e2e suites.
9. Don't test boundaries the current requirement didn't ask for.
10. Don't let green tests justify more abstraction.

Before adding any test, answer:

- Which accepted requirement does this test verify?
- Without it, would existing tests miss this regression?
- Is it simpler than the implementation?

If test code is longer or more complex than the implementation, treat it as overengineering.

## Model Allocation

- Requirement clarification and plan review: stronger model
- Writing/changing code, running tests: medium-low model or lighter execution model
- If the execution model starts stacking architecture or expanding scope: stop, rewrite a minimal plan

## Pre-Completion Checklist

- Restated intent and acceptance criteria
- Solution is the minimum approach, not the maximum
- Non-goals are marked
- Read relevant code directly instead of guessing
- Only changed the minimum file set needed
- Ran related existing tests
- Didn't add tests for scenarios that weren't requested
- Any new tests only lock current behavior, count is low
- Tests didn't introduce new dependencies or directory structures
- Diff is small, no extra files, no leftover debug code
- Didn't do extra work just to look complete
