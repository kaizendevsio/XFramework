# XFramework Agent Entry

Start with [CLAUDE.md](CLAUDE.md) for the concise AI-agent quickstart, then follow the canonical docs it links.

Primary route:

- [CLAUDE.md](CLAUDE.md) - current stack, authority hierarchy, before-coding checklist, and stale-pattern warnings.
- [docs/README.md](docs/README.md) - repository documentation map.
- [docs/solutions/README.md](docs/solutions/README.md) - durable solution knowledgebase and category index.
- [docs/solutions/conventions/xframework-best-practices.md](docs/solutions/conventions/xframework-best-practices.md) - canonical implementation standard.
- [rules/BackendGuidelines.md](rules/BackendGuidelines.md) - mandatory backend rules for the single-database, schema-per-module architecture.
- [docs/solutions/developer-experience/portal-service-wrapper-and-integration-test-contract.md](docs/solutions/developer-experience/portal-service-wrapper-and-integration-test-contract.md) - Portal wrapper vs `IDataContext` rules and integration-test coverage tiers.
- [docs/solutions/developer-experience/portal-feature-rcl-architecture.md](docs/solutions/developer-experience/portal-feature-rcl-architecture.md) - Portal host, shared UI, feature RCL, routing, and dependency-direction rules.
- [rules/UiGuidelines.md](rules/UiGuidelines.md) - primary UI rules for Portal and Blazor surfaces.

Backend rule:

- Read [rules/BackendGuidelines.md](rules/BackendGuidelines.md) before changing API modules, backend services, EF Core entities/configurations, migrations, database/runtime configuration, service wrappers, caching, or remote `IDataContext` behavior.
- XFramework uses one physical PostgreSQL database with schema-per-module separation. Do not introduce database-per-service or direct cross-module schema writes unless an explicit architecture decision approves the exception.

Portal UI rule:

- Read [rules/UiGuidelines.md](rules/UiGuidelines.md) before changing Portal or Blazor UI.

Portal table rule:

- Use BlazorBlueprint `BbDataGrid` for list, report, and data-heavy tabular UI as much as possible. Do not create raw HTML tables or custom table components unless the BlazorBlueprint grid cannot support the workflow, and document that exception in the change.
- Enable native `BbDataGrid` column filtering on useful user-facing data columns. Use `Filterable="true"` on property columns and set `FilterBy` on template columns; leave command/action columns unfiltered.

Task-specific OpenCode skills live under `.opencode/skills/`; use them as workflow helpers, not as the primary documentation authority.
