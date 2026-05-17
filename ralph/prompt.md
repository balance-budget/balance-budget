# ISSUES

**List issues** from the issue tracker and **Read** them to understand the open issues.

You will work on the AFK tasks only, not the HITL ones.

You've also been passed a file containing the last few commits.
Review these to understand what work has been done.

If all AFK tasks are complete, output <promise>NO MORE ISSUES</promise>

# TASK SELECTION

Pick the next task. Prioritize tasks in this order

1. Critical bug fixes
2. Development infrastructure

Getting development infrastucture like tests and types and dev scripts ready is an important precursor to building features.

3. Tracer bullets for new features

Tracer bullets are small slices of functionality that go through all layers of the system, allowing you to test and validate your approach early. This helps in identifiying potential issues and ensures that the overall architecture is sound before investing significant time in development.

TL;DR - build a tiny, end-to-end slice of the feature first, then expand it out.

4. Polish and quick wins
5. Refactors

# EXPLORATION

Explore the repo.

# IMPLEMENTATION

Use /tdd to complete the task

# FEEDBACK LOOPS

Before comitting, run the feedback loops:

- `dotnet csharpier format .` to format the code
- `dotnet test` to run the tests

# COMMIT

Make a git commit. The commit message must:

1. Include key decisions made
2. Include files changed
3. Blockers or notes for the next iteration

# THE ISSUE

If the task is complete **create a PR** and output <promise>ISSUE COMPLETED</promise>

If the task is not complete, **comment on the issue** with any blockers