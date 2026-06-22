# XFramework Agent Entry

Start with [CLAUDE.md](CLAUDE.md) for the concise AI-agent quickstart, then follow the canonical docs it links.

Primary route:

- [CLAUDE.md](CLAUDE.md) - current stack, authority hierarchy, before-coding checklist, and stale-pattern warnings.
- [docs/README.md](docs/README.md) - repository documentation map.
- [docs/solutions/README.md](docs/solutions/README.md) - durable solution knowledgebase and category index.
- [docs/solutions/conventions/xframework-best-practices.md](docs/solutions/conventions/xframework-best-practices.md) - canonical implementation standard.
- [docs/solutions/developer-experience/controlpanel-service-wrapper-and-integration-test-contract.md](docs/solutions/developer-experience/controlpanel-service-wrapper-and-integration-test-contract.md) - ControlPanel wrapper vs `IDataContext` rules and integration-test coverage tiers.
- [docs/solutions/tooling-decisions/blazor-blueprint-controlpanel-agent-guide.md](docs/solutions/tooling-decisions/blazor-blueprint-controlpanel-agent-guide.md) - BlazorBlueprint ControlPanel usage, docs lookup, and visual verification rules.

ControlPanel table rule:

- Use BlazorBlueprint `BbDataGrid` for list, report, and data-heavy tabular UI as much as possible. Do not create raw HTML tables or custom table components unless the BlazorBlueprint grid cannot support the workflow, and document that exception in the change.
- Enable native `BbDataGrid` column filtering on useful user-facing data columns. Use `Filterable="true"` on property columns and set `FilterBy` on template columns; leave command/action columns unfiltered.

Task-specific OpenCode skills live under `.opencode/skills/`; use them as workflow helpers, not as the primary documentation authority.
