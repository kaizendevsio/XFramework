# XFramework AI-Agent Quickstart

Use this file to orient quickly, then follow the canonical docs. Do not treat this as a full implementation manual.

## Current Stack

- .NET 10 and C# 14, with central package versions in `Directory.Packages.props`.
- Feature-centric Vertical Slice Architecture (VSA) with generated Minimal API registration.
- Bolt is the active RPC/streaming transport; use `[BoltHandler]` and `IBoltRequest` when a feature needs Bolt handling.
- Source generators produce endpoint routes, Bolt handlers, service wrappers, entity endpoints/services, and data-context registration.
- EF Core uses `AppDbContext`; caching uses XFramework cache services; logging uses ZLogger with OpenTelemetry.

## Authority Hierarchy

- Current source code wins when docs conflict with implementation.
- Root entrypoints route agents; they do not define detailed standards.
- [docs/README.md](docs/README.md) maps repository documentation.
- [docs/solutions/README.md](docs/solutions/README.md) maps the durable solution knowledgebase.
- [Best practices](docs/solutions/conventions/xframework-best-practices.md) is the primary implementation standard.
- Specialized current docs override general docs for their subsystem.
- Historical plans, migration notes, and superseded root markdown are context only.

## Before Coding

- Identify the owning module or surface in [the architecture map](docs/solutions/architecture-patterns/xframework-agent-architecture-surface-map.md) or [feature surface map](docs/solutions/conventions/xframework-feature-surface-map.md).
- Open a representative source path before editing.
- Read [best practices](docs/solutions/conventions/xframework-best-practices.md) and the most specific subsystem doc.
- Preserve user changes and avoid broad rewrites unless requested.
- Add or update tests when behavior changes; documentation-only work uses link and stale-term checks instead.

## Canonical Docs

- VSA execution: [VSA agent playbook](docs/solutions/conventions/xframework-vsa-agent-playbook.md).
- Generated endpoints: [generated endpoint auto-discovery](docs/solutions/tooling-decisions/generated-endpoint-auto-discovery.md) and [GenerateEndpoints usage](docs/solutions/tooling-decisions/generate-endpoints-attribute-usage.md).
- Bolt: [Bolt unified transport layer](docs/solutions/architecture-patterns/bolt-unified-transport-layer.md), [Bolt SignalR removal](docs/solutions/architecture-patterns/bolt-signalr-removal.md), `src/Libraries/Bolt/BOLT.md`, and `src/Libraries/Bolt/BOLT-MEDIA.md`.
- Backend/database boundaries: [backend guidelines](rules/BackendGuidelines.md).
- EF Core/data access: [EF Core data access patterns](docs/solutions/conventions/ef-core-data-access-patterns.md).
- ControlPanel API usage and broad integration tests: [ControlPanel service wrapper and integration test contract](docs/solutions/developer-experience/controlpanel-service-wrapper-and-integration-test-contract.md).
- Caching: [XFramework caching strategy](docs/solutions/best-practices/xframework-caching-strategy.md).
- Logging/observability: [logging standards](docs/solutions/conventions/logging-standards.md), [unified ZLogger logging pipeline](docs/solutions/architecture-patterns/unified-zlogger-logging-pipeline.md), and [OpenTelemetry integration guide](docs/solutions/tooling-decisions/opentelemetry-integration-guide.md).

## Stale Pattern Warnings

- Do not introduce StreamFlow or SignalR as current module RPC architecture; existing mentions should be historical, comparative, or removal context.
- Do not introduce MediatR/CQRS handlers or mediator dispatch for new features; use direct VSA endpoint/service patterns.
- Do not use Serilog as current logging guidance; use ZLogger and OpenTelemetry docs.
- Do not follow .NET 9 or C# 13 assumptions from old root markdown or workflow files.

## Expected Routes

- `AGENTS.md` -> `CLAUDE.md` -> `docs/solutions/conventions/xframework-best-practices.md`; backend work also reads `rules/BackendGuidelines.md`.
- `README.md` -> `docs/README.md` -> `docs/solutions/README.md`.
- `.github/copilot-instructions.md` -> VSA, Bolt, and source-generator guidance in `docs/solutions/`.
