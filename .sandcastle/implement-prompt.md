# TASK

Fix issue {{TASK_ID}}: {{ISSUE_TITLE}}

Pull in the issue using `gh issue view <ID>`. If it has a parent PRD, pull that in too.

Only work on the issue specified.

Work on branch {{BRANCH}}. Make commits and run tests.

# CONTEXT

Here are the last 10 commits:

<recent-commits>

!`git log -n 10 --format="%H%n%ad%n%B---" --date=short`

</recent-commits>

# EXPLORATION

Explore the repo and fill your context window with relevant information that will allow you to complete the task.

Pay extra attention to test files that touch the relevant parts of the code.

# EXECUTION

Follow the repo conventions in `CLAUDE.md` and `docs/conventions.md`. If applicable,
use red-green-refactor to complete the task (the backend suite is TUnit):

1. RED: write one test
2. GREEN: write the implementation to pass that test
3. REPEAT until done
4. REFACTOR the code

# FEEDBACK LOOPS

Before committing, run the checks for the layers you touched and make sure they pass.

Backend (C#) — if you changed any `.cs`:

- `dotnet csharpier check . --ignore-path .csharpierignore` (CI fails on any deviation; run `dotnet csharpier format .` to fix)
- `dotnet build --no-restore -v:minimal` (`TreatWarningsAsErrors=true`, so warnings fail the build)
- `dotnet test --no-build -v:minimal`

Frontend (SPA) — if you changed anything under `src/Balance.Web.Client`:

- `npm run typecheck`
- `npm run lint`
- `npm run test`

If you changed the backend API surface, also run `npm run codegen` and commit the
regenerated client — CI gates on `.gen.ts` drift.

# COMMIT

Make a git commit following Conventional Commits (`type(scope): summary` in the
imperative mood) as required by `CLAUDE.md` — e.g. `feat(reports): add monthly outlook`.
Valid types: `feat`, `fix`, `docs`, `chore`, `refactor`, `style`, `test`, `ci`,
`build`, `perf`, `revert`. Reference the issue in a footer (`Refs: #NN`), not the
subject. Keep the subject concise; use the body for key decisions and any notes for
the next iteration.

# THE ISSUE

If the task is not complete, leave a comment on the issue with what was done.

Do not close the issue - this will be done later.

Once complete, output <promise>COMPLETE</promise>.

# FINAL RULES

ONLY WORK ON A SINGLE TASK.
