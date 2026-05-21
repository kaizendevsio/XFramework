---
title: "XFramework Agent Architecture Surface Map"
date: 2026-05-21
category: architecture-patterns
module: XFramework
problem_type: architecture
component: agent_onboarding
severity: high
applies_when:
  - "Orienting an agent or developer before changing XFramework code"
  - "Finding the current owner doc or representative source path for an architecture surface"
tags: [architecture, agent-onboarding, vsa, bolt, modules]
status: current
---

# XFramework Agent Architecture Surface Map

Use this map to answer "where should I look first?" It is intentionally navigational: detailed implementation rules remain in the canonical docs linked below.

## Canonical Docs

| Need | Start here |
|---|---|
| Current coding and review standards | [Best practices](../conventions/xframework-best-practices.md) |
| VSA task execution | [VSA agent playbook](../conventions/xframework-vsa-agent-playbook.md) |
| Feature and module inventory | [Feature surface map](../conventions/xframework-feature-surface-map.md) |
| Source-generated endpoints | [Generated endpoint auto-discovery](../tooling-decisions/generated-endpoint-auto-discovery.md) and [GenerateEndpoints attribute usage](../tooling-decisions/generate-endpoints-attribute-usage.md) |
| Bolt transport | [Bolt unified transport layer](bolt-unified-transport-layer.md), [Bolt SignalR removal](bolt-signalr-removal.md), `src/Libraries/Bolt/BOLT.md`, `src/Libraries/Bolt/BOLT-MEDIA.md` |
| Data-context and sync architecture | [Decentralized remote data context](decentralized-remote-data-context.md), [Local-first sync architecture](local-first-sync-architecture.md) |
| Caching | [XFramework caching strategy](../best-practices/xframework-caching-strategy.md) |
| Logging and observability | [Unified ZLogger logging pipeline](unified-zlogger-logging-pipeline.md), [Logging standards](../conventions/logging-standards.md), [OpenTelemetry integration guide](../tooling-decisions/opentelemetry-integration-guide.md) |
| UI editing patterns | [Control panel editable UI patterns](../design-patterns/control-panel-editable-ui-patterns.md) |
| Architecture risks and hardening | [Architecture hardening](xframework-architecture-hardening.md) |

## Repository Shape

`XFramework.slnx` is the current solution map. It groups the repo into Shared, Kernel, Infrastructure, SourceGenerators, Services, Presentation, Libraries/Bolt, Tests, and Tools.

| Surface | Role | Representative paths |
|---|---|---|
| Shared | Cross-module contracts and attributes that stay lightweight | `src/Shared/XFramework.Domain.Shared/XFramework.Domain.Shared.csproj`, `src/Shared/XFramework.Domain.Shared/Attributes/GenerateEndpointsAttribute.cs` |
| Kernel/Core | Cross-cutting runtime primitives, endpoint discovery, caching, observability, and result/data-context infrastructure | `src/Kernel/XFramework.Core/XFramework.Core.csproj`, `src/Kernel/XFramework.Core/Extensions/EndpointDiscoveryExtensions.cs`, `src/Kernel/XFramework.Core/Services/Caching/HybridCacheService.cs`, `src/Kernel/XFramework.Core/Extensions/OpenTelemetryExtensions.cs` |
| Kernel/Domain | EF Core domain context and persistence base | `src/Kernel/XFramework.Domain/XFramework.Domain.csproj`, `src/Kernel/XFramework.Domain/Contexts/AppDbContext.cs`, `src/Kernel/XFramework.Domain/Contexts/XDbContext.cs` |
| Infrastructure | Integration attributes, remote data-context, wrappers, logging extensions, and Bolt helper surfaces | `src/Infrastructure/XFramework.Integration/XFramework.Integration.csproj`, `src/Infrastructure/XFramework.Integration/Attributes/MapEndpointAttributes.cs`, `src/Infrastructure/XFramework.Integration/Attributes/BoltHandlerAttribute.cs`, `src/Infrastructure/XFramework.Integration/DataContext/RemoteDataContext.cs`, `src/Infrastructure/XFramework.Integration/Extensions/LoggingExtensions.cs` |
| SourceGenerators | Build-time generation for endpoints, services, wrappers, Bolt handlers, data-context registration, and change tracking | `src/SourceGenerators/XFramework.SourceGenerators/XFramework.SourceGenerators.csproj`, `src/SourceGenerators/XFramework.SourceGenerators/EntityEndpointGenerator.cs`, `src/SourceGenerators/XFramework.SourceGenerators/BoltHandlerGenerator.cs`, `src/SourceGenerators/XFramework.SourceGenerators/DataContextRegistrationGenerator.cs` |
| Modules/Services | Business modules and service apps | `src/Modules/XFramework.IdentityServer/IdentityServer.Api/IdentityServer.Api.csproj`, `src/Modules/XFramework.Wallets/Wallets.Api/Wallets.Api.csproj`, `src/Modules/XFramework.Bolt/Bolt.Hub/Bolt.Hub.csproj` |
| Libraries/Bolt | Reusable Bolt protocol, server, client, media, and browser packages | `src/Libraries/Bolt/Bolt.Protocol/Bolt.Protocol.csproj`, `src/Libraries/Bolt/Bolt.Client/Bolt.Client.csproj`, `src/Libraries/Bolt/Bolt.Server/Bolt.Server.csproj`, `src/Libraries/Bolt/Bolt.Media/Bolt.Media.csproj`, `src/Libraries/Bolt/Bolt.Media.Browser/Bolt.Media.Browser.csproj` |
| Presentation | Blazor/control panel, gateway, and app shells | `src/Modules/XFramework.Blazor/XFramework.Blazor.csproj`, `src/Presentation/ControlPanel.Server/ControlPanel.Server.csproj`, `src/Presentation/Gateway/Gateway.csproj`, `src/Presentation/Fluid/Fluid.csproj` |
| Tests | Unit, integration, E2E, benchmarks, and test infrastructure | `src/Tests/Bolt.Tests/Bolt.Tests.csproj`, `src/Tests/XFramework.Core.Tests/XFramework.Core.Tests.csproj`, `src/Tests/IdentityServer.IntegrationTests/IdentityServer.IntegrationTests.csproj`, `src/Tests/Wallets.IntegrationTests/Wallets.IntegrationTests.csproj`, `src/Tests/ControlPanel.E2ETests/ControlPanel.E2ETests.csproj` |
| Tools | Operational tools, especially migrations | `src/Tools/XFramework.MigrationRunner/XFramework.MigrationRunner.csproj` |
| Docs | Current knowledgebase and historical execution plans | `docs/README.md`, `docs/solutions/README.md`, `docs/plans/README.md` |
| Build/config | SDK, central package versions, package metadata, container image, and publish workflow | `global.json`, `Directory.Packages.props`, `Directory.Build.props`, `Version.props`, `Dockerfile`, `.github/workflows/publish.yml` |
| Historical/deferred workflow config | Legacy StreamFlow deployment surface retained as config evidence, not current architecture guidance | `.workflows/streamflow.service.yaml` references historical/deferred `src/Modules/XFramework.StreamFlow/StreamFlow.Stream/StreamFlow.Stream.csproj` |

## Current Stack Anchors

| Concern | Evidence path |
|---|---|
| .NET SDK | `global.json` pins SDK `10.0.0`; `.github/workflows/publish.yml` uses `10.0.x`; `Dockerfile` uses .NET SDK/ASP.NET runtime `10.0` images. |
| Central package versions | `Directory.Packages.props` owns package versions for ASP.NET Core, EF Core, OpenTelemetry, ZLogger, FluentValidation, testing, and Bolt dependencies. |
| Package versions | `Version.props` owns `XFrameworkVersion` and `BoltVersion`; `Directory.Build.props` imports it and holds shared NuGet metadata. |
| Solution topology | `XFramework.slnx` is the highest-signal inventory of active solution projects. |

## Feature Architecture Surfaces

| Surface | Where to start | Notes |
|---|---|---|
| Manual VSA endpoints | `src/Modules/XFramework.IdentityServer/IdentityServer.Api/Features/Auth/Authenticate/Endpoint.cs`, `src/Modules/XFramework.Wallets/Wallets.Api/Features/Wallets/Transfer/Endpoint.cs`, `src/Modules/XFramework.Messaging/Messaging.Api/Features/Messages/CreateMessage/Endpoint.cs`, `src/Modules/XFramework.Community/Community.Api/Features/Content/Create/Endpoint.cs`, `src/Modules/XFramework.Inventario/Inventario.Api/Features/Products/Create/Endpoint.cs` | Use the VSA playbook and best-practices docs for rules. |
| Generated entity CRUD | `src/Shared/XFramework.Domain.Shared/Attributes/GenerateEndpointsAttribute.cs`, `src/Infrastructure/XFramework.Integration/Attributes/MapEndpointAttributes.cs`, `src/Kernel/XFramework.Core/Extensions/EndpointDiscoveryExtensions.cs` | Declaration and registration details belong to the generated endpoint docs. |
| Bolt handlers and contracts | `src/Infrastructure/XFramework.Integration/Attributes/BoltHandlerAttribute.cs`, `src/Infrastructure/XFramework.Integration/Abstractions/IBoltHandler.cs`, `src/Modules/XFramework.Bolt/Bolt.Domain.Shared/Contracts/Requests/IBoltRequest.cs`, `src/SourceGenerators/XFramework.SourceGenerators/BoltHandlerGenerator.cs` | Bolt is the active transport surface; SignalR references are historical or comparative unless a current doc says otherwise. |
| Data access | `src/Kernel/XFramework.Domain/Contexts/AppDbContext.cs`, `src/Kernel/XFramework.Core/DataContext/ServerDataContext.cs`, `src/Infrastructure/XFramework.Integration/DataContext/RemoteDataContext.cs`, `src/Tools/XFramework.MigrationRunner/Program.cs` | Use data-context docs for deeper behavior. |
| Caching | `src/Kernel/XFramework.Core/Services/Caching/HybridCacheService.cs`, `src/Infrastructure/XFramework.Integration/DataContext/Cache/ClientCacheService.cs`, `src/Infrastructure/XFramework.Integration/DataContext/Cache/CacheKeyBuilder.cs` | Cache key and invalidation details belong to the caching strategy doc. |
| Observability | `src/Infrastructure/XFramework.Integration/Extensions/LoggingExtensions.cs`, `src/Infrastructure/XFramework.Integration/Logging/ZLoggerSeqSink.cs`, `src/Kernel/XFramework.Core/Observability/ActivitySources.cs`, `src/Kernel/XFramework.Core/Middlewares/CorrelationIdMiddleware.cs` | Use the logging and OpenTelemetry docs for detailed standards. |
| Blazor state/actions | `src/Modules/XFramework.Blazor/Core/Features/BaseActionHandler.cs`, `src/Modules/XFramework.Blazor/Core/Services/IndexedDbService.cs` | The module provides reusable UI/client state and IndexedDB surfaces. |
| Control panel pages | `src/Presentation/ControlPanel.Server/Program.cs`, `src/Presentation/ControlPanel.Server/Components/Pages/Finance/Wallets.razor`, `src/Presentation/ControlPanel.Server/Components/Pages/Identity/Users.razor` | UI implementation details belong to presentation docs and patterns. |
| Bolt protocol and media | `src/Libraries/Bolt/Bolt.Protocol/Protocol/BoltCodec.cs`, `src/Libraries/Bolt/Bolt.Client/BoltClient.cs`, `src/Libraries/Bolt/Bolt.Server/BoltServer.cs`, `src/Libraries/Bolt/Bolt.Media/BoltMediaClient.cs`, `src/Libraries/Bolt/Bolt.Media.Browser/BoltMediaService.cs` | Start with `src/Libraries/Bolt/BOLT.md` and `src/Libraries/Bolt/BOLT-MEDIA.md`. |

## Build, Deployment, And Runtime Config Surfaces

| Surface | Current status |
|---|---|
| `Dockerfile` | Current generic .NET 10 publish image. It accepts `PROJECT_PATH` and writes the entrypoint DLL during publish. |
| `.github/workflows/publish.yml` | Current GitHub Actions NuGet publish workflow for `main` and `develop`; it builds `XFramework.slnx`, tests core tests with `continue-on-error`, packs, and pushes packages. |
| `.workflows/streamflow.service.yaml` | Historical/deferred Azure pipeline config. It still targets a removed StreamFlow project and .NET 9, so do not treat it as current implementation guidance without a separate modernization task. |

## Orientation Checklist

Before changing code, identify the layer from `XFramework.slnx`, open the matching representative source path above, then follow the most specific canonical doc. If a doc conflicts with current source, source wins; if a historical or migration doc conflicts with current conventions, use the current convention or subsystem doc.
