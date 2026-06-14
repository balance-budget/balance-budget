# ISSUES

Here are the open issues in the repo:

<issues-json>

!`gh issue list --state open --label ready-for-agent --json number,title,body,labels,comments --jq '[.[] | {number, title, body, labels: [.labels[].name], comments: [.comments[].body]}]'`

</issues-json>

The list above is filtered to issues carrying the `ready-for-agent` triage label
(see `docs/agents/triage-labels.md`) — issues that are fully specified and cleared
for an AFK agent to pick up.

# ALREADY IN FLIGHT

These pull requests are already open. Each `sandcastle/issue-{id}-{slug}` head branch
encodes the issue it implements, and `closingIssuesReferences` lists the issues a PR
will close on merge:

<open-prs-json>

!`gh pr list --state open --json number,title,headRefName,closingIssuesReferences --jq '[.[] | {number, title, headRefName, closes: [.closingIssuesReferences[].number]}]'`

</open-prs-json>

Any issue that already has an open PR (matched by `closes`, or by the issue id in the
`headRefName`) is **in flight** — exclude it from your plan entirely so the same work
is not started twice.

# TASK

Analyze the open issues and build a dependency graph. For each issue, determine whether it **blocks** or **is blocked by** any other open issue.

An issue B is **blocked by** issue A if:

- B requires code or infrastructure that A introduces
- B and A modify overlapping files or modules, making concurrent work likely to produce merge conflicts
- B's requirements depend on a decision or API shape that A will establish

An issue is **unblocked** if it has zero blocking dependencies on other open issues
**and** is not already in flight (has no open PR — see the section above).

For each unblocked issue, assign a branch name using the format `sandcastle/issue-{id}-{slug}`.

# OUTPUT

Output your plan as a JSON object wrapped in `<plan>` tags:

<plan>
{"issues": [{"id": "42", "title": "Fix auth bug", "branch": "sandcastle/issue-42-fix-auth-bug"}]}
</plan>

Include only unblocked issues. If every issue is blocked, include the single highest-priority candidate (the one with the fewest or weakest dependencies).
