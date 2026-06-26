---
description: Create a PR against develop
agent: build
---

# Create PR Against Develop

Create a GitHub pull request from the current work branch to `develop`.

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

Rules:
- Target `develop` unless the user explicitly overrides the base branch.
- Do not merge the PR in this command.
- Do not include unrelated or user-owned changes in the commit.
- Report the PR URL, branch name, commit hash, and any tests run.
