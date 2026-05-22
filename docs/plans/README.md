# Plan Archive

`docs/plans/` stores implementation plans and execution checklists. Plans preserve intent, sequencing, tradeoffs, and historical context; they are not the canonical place for current implementation conventions.

## Filename Convention

Plan filenames use this shape:

```text
YYYY-MM-DD-NNN-type-short-title-plan.md
```

Parts:

- `YYYY-MM-DD` - date the plan was created.
- `NNN` - daily sequence number, starting at `001`.
- `type` - change type, such as `feat`, `fix`, `refactor`, `docs`, or `chore`.
- `short-title` - lowercase hyphenated summary.
- `plan.md` - suffix that marks the file as a plan artifact.

Example: `docs/plans/2026-05-21-001-refactor-ai-agent-knowledgebase-plan.md`.

## Active Vs Historical

- A plan is active only when its YAML frontmatter explicitly marks it active, for example `status: active`.
- Old plan files are historical unless their frontmatter marks them active.
- Historical plans should not be rewritten just to match current terminology. Add current guidance in `docs/solutions/` and link to it from current entry points instead.
- If a historical plan contains stale terms such as former transports, packages, or migration targets, read them as context for that plan's date and scope, not as current instructions.

## Frontmatter

Plans usually include YAML frontmatter like:

```yaml
---
title: refactor: Refresh AI Agent Knowledgebase
type: refactor
status: active
date: 2026-05-21
---
```

Common fields:

- `title` - readable plan title.
- `type` - change type.
- `status` - `active`, `draft`, `completed`, `paused`, or omitted for historical records.
- `date` - creation date in `YYYY-MM-DD` format.
- `deepened` - optional date for a later planning refinement pass.

## How To Use Plans

- Start with active plans when implementing queued work.
- Use historical plans to understand why a change was made or how a migration was sequenced.
- Do not treat a historical plan as more authoritative than source code or current solution docs.
- Create new plans with `/ce-plan` when a task needs structured planning before implementation.
