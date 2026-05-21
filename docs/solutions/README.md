---
title: "Solution Knowledgebase"
date: 2026-05-21
category: conventions
module: XFramework
problem_type: convention
component: documentation
severity: high
applies_when:
  - "Finding current XFramework solution guidance and metadata expectations"
  - "Adding or updating durable docs under docs/solutions"
tags: [knowledgebase, documentation, agent-onboarding]
status: current
---

# Solution Knowledgebase

`docs/solutions/` is the durable XFramework knowledgebase. Use it for guidance that should be searchable, reusable, and discoverable by humans and agents.

## Authority And Status

- Current source code wins when implementation and documentation disagree.
- Current implementation conventions belong in `docs/solutions/conventions/`, with `docs/solutions/conventions/xframework-best-practices.md` as the primary general standard.
- Specialized subsystem guidance belongs in the most specific matching solution doc.
- Historical, migration, or comparative docs can remain useful, but they do not override current conventions or source code.
- When a document is superseded, state that in its frontmatter or opening section and link to the current replacement.
- Historical root markdown files listed in `docs/README.md` are project memory only. Do not use them as implementation authority for .NET/C# version, VSA, transport, logging, or architecture decisions.

## Categories

- `docs/solutions/architecture-patterns/` - architecture decisions, subsystem shape, transport/data patterns, hardening notes, and historical architecture migrations.
- `docs/solutions/best-practices/` - focused implementation best practices for specific cross-cutting concerns.
- `docs/solutions/conventions/` - current coding, VSA, logging, migration, and agent-facing conventions.
- `docs/solutions/design-patterns/` - reusable UI and application design patterns.
- `docs/solutions/developer-experience/` - developer workflow, migration ergonomics, and local productivity guidance.
- `docs/solutions/tooling-decisions/` - source generation, endpoint discovery, observability tooling, and other tool choices.
- `docs/solutions/workflow-issues/` - validation checklists, production readiness notes, and issue-oriented workflow records.

## Current Orientation Maps

- [XFramework agent architecture surface map](architecture-patterns/xframework-agent-architecture-surface-map.md) - repository architecture surfaces, current stack anchors, and where to look first.
- [XFramework feature surface map](conventions/xframework-feature-surface-map.md) - module inventory, representative feature paths, and historical/deferred path notes.

## Current Subsystem Guidance

- [EF Core data access patterns](conventions/ef-core-data-access-patterns.md) - `AppDbContext` discovery, module configurations, migrations, tests, and local/remote `IDataContext` behavior.
- [XFramework caching strategy](best-practices/xframework-caching-strategy.md) - custom `HybridCacheService`, Redis/distributed cache, remote data-context client cache, module-local caches, and generated endpoint cache metadata.
- [OpenTelemetry integration guide](tooling-decisions/opentelemetry-integration-guide.md) - tracing, metrics, resources, exporters, and log correlation.
- [Logging standards](conventions/logging-standards.md) - structured logging conventions for the current ZLogger pipeline.
- [Unified ZLogger logging pipeline](architecture-patterns/unified-zlogger-logging-pipeline.md) - current logging decision record and historical Serilog removal context.
- [Decentralized remote data context](architecture-patterns/decentralized-remote-data-context.md) - remote `IDataContext` architecture over generated service wrappers.
- [Blazor IDataContext migration](developer-experience/blazor-idatacontext-migration.md) - migrating Blazor generated CRUD wrapper calls to `IDataContext`.

## Current Vs Historical Guidance

- Treat docs with current language, active frontmatter, or convention-focused titles as current guidance.
- Treat migration plans, removal narratives, readiness notes, and dated troubleshooting writeups as historical unless they explicitly say they are current.
- Do not rewrite historical docs only to remove stale terminology; instead, label the status when needed and link to current guidance.
- If docs and implementation disagree, current source code wins for factual behavior. Among docs, prefer the most specific current subsystem doc, then `docs/solutions/conventions/xframework-best-practices.md`.
- Treat root files named `*Roadmap*`, `*Improvement-Plan*`, `*Journal*`, `*Completion*`, or `XFramework-Knowledge-Base.md` as historical or superseded unless their opening status notice says otherwise.

## Frontmatter Schema

Solution docs should use YAML frontmatter so tools can search and classify them consistently.

Required or expected fields:

- `title` - human-readable document title.
- `date` - creation or last material update date in `YYYY-MM-DD` format.
- `category` - one of the solution categories, such as `architecture-patterns`, `best-practices`, `conventions`, `design-patterns`, `developer-experience`, `tooling-decisions`, or `workflow-issues`.
- `module` - affected module or `XFramework` for repository-wide guidance.
- `problem_type` - the kind of knowledge captured, such as `convention`, `architecture`, `tooling`, `workflow`, `migration`, or `bug`.
- `component` - subsystem or surface area, such as `assistant`, `development_workflow`, `logging`, `source_generators`, `transport`, or `data_access`.
- `severity` - relative importance for discovery, usually `low`, `medium`, `high`, or `critical`.
- `applies_when` - list of situations where the document should be consulted.
- `tags` - short searchable keywords.

Optional status fields may be added when useful:

- `status` - `current`, `active`, `historical`, `superseded`, or `deprecated`.
- `superseded_by` - repo-relative path to the replacement guidance.

Example:

```yaml
---
title: "XFramework Best Practices and Standards"
date: 2026-03-12
category: conventions
module: XFramework
problem_type: convention
component: development_workflow
severity: high
applies_when:
  - "Creating, refactoring, or reviewing XFramework features"
tags: [vsa, standards, conventions]
---
```

## Maintaining This Index

This index maps the existing solution categories and metadata contract. Add links to newly created solution docs only after those docs exist.
