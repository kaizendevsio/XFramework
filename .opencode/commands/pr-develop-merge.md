---
description: Create a PR against develop and merge with admin bypass
agent: build
---

# Create PR Against Develop And Admin Merge

Create a GitHub pull request from the current work branch to `develop`, then merge it using admin bypass.

Arguments: `$ARGUMENTS` may include the desired PR title, commit message, or short context for the PR body. If arguments are empty, infer a concise title and summary from the diff and recent commits.

Workflow:
- Run `git fetch origin develop` and inspect `git status --short --branch`.
- Confirm the current branch is not `develop`, `main`, or `master`. If it is, create a focused `codex/<short-task-name>` branch from `origin/develop` before committing.
- If there are uncommitted changes, review the diff, stage only relevant files, and create one concise commit.
- If there are no local changes and no unpushed commits, stop and report that there is nothing to open a PR for.
- Push the branch with `git push -u origin <branch>`.
- Create the PR with `gh pr create --base develop --head <branch>`.
- Use a PR body with `## Summary` and `## Testing` sections.
- Verify the PR with `gh pr view --json number,url,state,baseRefName,headRefName`.
- Merge the PR with `gh pr merge <number-or-url> --merge --admin`.
- Verify the merge with `gh pr view <number-or-url> --json number,url,state,mergedAt,mergeCommit,baseRefName,headRefName`.

Rules:
- Target `develop` unless the user explicitly overrides the base branch.
- Admin bypass is intentional for this command; do not wait for required checks unless the user asks.
- Do not use `--delete-branch` during merge from a worktree. Delete the remote branch separately only when safe or requested.
- Do not include unrelated or user-owned changes in the commit.
- Report the PR URL, merge commit hash, branch name, and any tests run.
