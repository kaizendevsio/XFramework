# Copilot Instructions: XFramework

Prefer XFramework-specific guidance over generic .NET layered-architecture advice.

## Current Architecture

- Target .NET 10 and C# 14.
- Organize API work by module-qualified Vertical Slice Architecture paths: `src/Modules/XFramework.[Module]/[Module].Api/Features/[FeatureGroup]/[Action]/Endpoint.cs`.
- Use generated Minimal API registration with `app.MapGeneratedEndpoints()`.
- Use Bolt for active RPC/streaming work with `[BoltHandler]` and `IBoltRequest`.
- Use source generators for endpoint routes, Bolt handlers, service wrappers, entity endpoints/services, and data-context registration.

## Start Here

- Repository docs map: [`docs/README.md`](../docs/README.md).
- Solution knowledgebase: [`docs/solutions/README.md`](../docs/solutions/README.md).
- Canonical implementation standard: [`docs/solutions/conventions/xframework-best-practices.md`](../docs/solutions/conventions/xframework-best-practices.md).
- VSA task guide: [`docs/solutions/conventions/xframework-vsa-agent-playbook.md`](../docs/solutions/conventions/xframework-vsa-agent-playbook.md).
- Architecture surface map: [`docs/solutions/architecture-patterns/xframework-agent-architecture-surface-map.md`](../docs/solutions/architecture-patterns/xframework-agent-architecture-surface-map.md).
- Feature/module map: [`docs/solutions/conventions/xframework-feature-surface-map.md`](../docs/solutions/conventions/xframework-feature-surface-map.md).

## Source Generator And Transport Docs

- Generated endpoint discovery: [`docs/solutions/tooling-decisions/generated-endpoint-auto-discovery.md`](../docs/solutions/tooling-decisions/generated-endpoint-auto-discovery.md).
- `GenerateEndpoints` usage: [`docs/solutions/tooling-decisions/generate-endpoints-attribute-usage.md`](../docs/solutions/tooling-decisions/generate-endpoints-attribute-usage.md).
- Bolt transport: [`docs/solutions/architecture-patterns/bolt-unified-transport-layer.md`](../docs/solutions/architecture-patterns/bolt-unified-transport-layer.md), [`docs/solutions/architecture-patterns/bolt-signalr-removal.md`](../docs/solutions/architecture-patterns/bolt-signalr-removal.md), `src/Libraries/Bolt/BOLT.md`, and `src/Libraries/Bolt/BOLT-MEDIA.md`.

## Do Not Suggest For New Work

- Generic layered folders such as `Controllers`, `Models`, `Repositories`, and `Services` as the primary architecture.
- MediatR/CQRS handlers or mediator dispatch.
- StreamFlow or SignalR as current module RPC architecture.
- Serilog as current logging guidance.
- .NET 9 or C# 13 assumptions from historical docs.

When unsure, route the user to the most specific current `docs/solutions/` document and existing source examples before generating code.
