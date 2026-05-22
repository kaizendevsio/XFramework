# XFramework Documentation

This is the repository documentation map. It points to the current knowledgebase and historical planning records without duplicating architecture or implementation guidance.

## Authority

- Current source code is the final authority when documentation and implementation disagree.
- Entry points such as `README.md`, `CLAUDE.md`, `AGENTS.md`, and `.github/copilot-instructions.md` orient readers and agents, but they should route to canonical docs instead of restating every rule.
- `docs/solutions/` holds the durable XFramework knowledgebase: current conventions, architecture patterns, best practices, design patterns, tooling decisions, developer experience notes, and workflow issue writeups.
- `docs/plans/` holds implementation plans and execution history. Plans explain intent and sequencing; they are not current implementation standards unless their frontmatter marks them active.
- Root-level historical markdown files are retained for project memory. Agents must not treat them as implementation authority unless a current `docs/solutions/` document explicitly points to them for context.

## Documentation Areas

- [`docs/solutions/README.md`](solutions/README.md) - category map for solution docs, current-vs-historical guidance, and required YAML frontmatter fields.
- [`docs/solutions/architecture-patterns/xframework-agent-architecture-surface-map.md`](solutions/architecture-patterns/xframework-agent-architecture-surface-map.md) - current architecture surface map for agents and maintainers.
- [`docs/solutions/conventions/xframework-feature-surface-map.md`](solutions/conventions/xframework-feature-surface-map.md) - current module and feature surface map with representative paths.
- [`docs/plans/README.md`](plans/README.md) - plan filename conventions, active status rules, and historical-plan handling.
- `docs/brainstorms/` - requirements and brainstorm artifacts created by `/ce-brainstorm` when needed.
- `.opencode/commands/` and `.opencode/skills/` - project-local OpenCode commands and auto-selectable skills for XFramework workflows.

## Consolidated Layout

The old `docs/superpowers/`, `docs/architecture/`, `docs/migration/`, `docs/observability/`, `docs/source-generators/`, and `docs/standards/` layouts have been consolidated into `docs/solutions/` and `docs/plans/` so Compound Engineering skills can discover the knowledge consistently.

When adding or updating guidance, prefer updating an existing canonical solution document before creating a new parallel document.

## Legacy Root Markdown Inventory

These files are intentionally small-status-labeled rather than rewritten. Use them for context only; current behavior and conventions live in source code and `docs/solutions/`.

| File | Classification | Agent handling |
|---|---|---|
| [`XFramework-Knowledge-Base.md`](../XFramework-Knowledge-Base.md) | Superseded legacy knowledge base | Historical snapshot only; stale .NET 9/C#13, CQRS/MediatR, SignalR/StreamFlow, and older logging references do not override current docs. |
| [`XFramework-Development-Roadmap.md`](../XFramework-Development-Roadmap.md) | Historical roadmap | Use for modernization timeline context, not current implementation standards. |
| [`XFramework-Improvement-Plan.md`](../XFramework-Improvement-Plan.md) | Superseded historical plan | Use for original improvement intent only; prefer current solution docs for active guidance. |
| [`PHASE3-VSA-MIGRATION-JOURNAL.md`](../PHASE3-VSA-MIGRATION-JOURNAL.md) | Historical migration journal | Use for VSA migration history only. |
| [`XFramework-Analysis-Journal-2025-01-24-1140.md`](../XFramework-Analysis-Journal-2025-01-24-1140.md) | Historical analysis journal | Point-in-time analysis; not current codebase status. |
| [`XFramework-Phase1-Completion-2025-01-24.md`](../XFramework-Phase1-Completion-2025-01-24.md) | Historical completion report | Project-history record; not implementation authority. |
| [`src/Modules/XFramework.Payments/Payments.Core/README.md`](../src/Modules/XFramework.Payments/Payments.Core/README.md) | Current module README | High-level Payments.Core overview; source code and current conventions remain authoritative. |
