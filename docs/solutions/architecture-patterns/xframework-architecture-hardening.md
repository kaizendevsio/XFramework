---
title: "XFramework Architecture Hardening"
date: 2026-04-25
category: architecture-patterns
module: XFramework
problem_type: architecture_pattern
component: development_workflow
severity: critical
status: historical
applies_when:
  - "Reviewing production readiness across modules, generated endpoints, Bolt transport, caching, secrets, tests, and deployment"
tags: [architecture, hardening, bolt, source-generators, secrets, caching]
---

# XFramework Architecture Improvement Report

**Status:** Historical point-in-time hardening review. Use this report as backlog/context, not as current architecture authority. For current implementation guidance, prefer current subsystem docs, especially `docs/solutions/conventions/xframework-best-practices.md`, `docs/solutions/architecture-patterns/xframework-agent-architecture-surface-map.md`, and the most specific current architecture/convention doc.

Date: 2026-04-25

Scope: read-only review of solution structure, module APIs, Bolt transport, source generators, shared infrastructure, persistence, tests, deployment, and configuration.

## Executive Summary

XFramework has a strong foundation: modular service projects, Vertical Slice Architecture, central package management, source-generated HTTP/Bolt endpoints, a custom high-performance Bolt transport, shared health/observability primitives, and integration tests that exercise realistic multi-app topology.

The biggest improvement areas are production hardening and architectural consistency. The highest-priority issues are committed secrets, source-generator fragility, Bolt stream/backpressure correctness, cache configuration correctness, Core-to-module coupling, and drift across module composition patterns.

## Current Architecture Strengths

- Modular solution layout is clear: `src/Modules`, `src/Libraries/Bolt`, `src/Kernel`, `src/Shared`, `src/Infrastructure`, `src/SourceGenerators`, `src/Tests`.
- Central Package Management is enabled in `Directory.Packages.props:2-4`.
- Shared package metadata/versioning exists in `Directory.Build.props:4-13` and `Version.props`.
- VSA endpoint style is mostly consistent, for example `src/Modules/XFramework.IdentityServer/IdentityServer.Api/Features/Auth/Authenticate/Endpoint.cs:6-19`.
- Request contracts support dual transport through MemoryPack and `IBoltRequest`, for example `src/Modules/XFramework.IdentityServer/IdentityServer.Domain.Shared/Contracts/Requests/AuthenticateIdentityRequest.cs:6-10`.
- Generated endpoint registration is used across modules, for example `IdentityServer.Api/Program.cs:51-52` and `Wallets.Api/Program.cs:50-51`.
- Bolt protocol has a well-defined binary framing layer in `src/Libraries/Bolt/Bolt.Protocol/Protocol/BoltCodec.cs:22-60`.
- Shared health checks expose `/health`, `/health/live`, and `/health/ready` in `src/Kernel/XFramework.Core/Health/XFrameworkHealthCheckExtensions.cs:58-94`.
- Integration tests spin up PostgreSQL, Bolt hub, service app, and test client, for example `src/Tests/IdentityServer.IntegrationTests/Infrastructure/IntegrationTestFixture.cs:57-97`.

## Highest Priority Improvements

### 1. Remove Committed Secrets

Priority: critical

Why it matters: secrets are present in committed config files. Even if these are development values, they should be treated as leaked once committed and rotated.

Evidence:

- JWT secret in `src/Modules/XFramework.IdentityServer/IdentityServer.Api/appsettings.json:49-52`.
- Gateway database password and encryption secrets in `src/Presentation/Gateway/appsettings.Development.json:17-31`.
- Default PostgreSQL password in `docker-compose.yml:19-21` and migration connection string in `docker-compose.yml:40-41`.
- Repeated secrets across module `appsettings*.json` files found by scan.

Recommended change:

- Move secrets to environment variables, user-secrets for local dev, or a secret provider.
- Keep committed files limited to non-secret placeholders.
- Rotate all values currently committed.
- Add secret scanning to CI and pre-commit hooks.

### 2. Add Source Generator Diagnostics and Semantic Matching

Priority: high

Why it matters: endpoint and wrapper generation can fail silently. This is risky because the architecture relies heavily on generated code.

Evidence:

- `BoltHandlerGenerator` scans methods and detects attributes using syntax string `Contains` in `src/SourceGenerators/XFramework.SourceGenerators/BoltHandlerGenerator.cs:71-91`.
- Invalid handler shapes are skipped without diagnostics in `BoltHandlerGenerator.cs:89-91` and `BoltHandlerGenerator.cs:121-126`.
- No generator tests were found under `src/Tests` for `BoltHandlerGenerator`, `ServiceWrapperGenerator`, or Roslyn `GeneratorDriver` usage.
- `ServiceWrapperGenerator` exits early when no CRUD models are found, before custom `IBoltRequest` methods can generate wrappers, in `src/SourceGenerators/XFramework.SourceGenerators/ServiceWrapperGenerator.cs:50-55`.

Recommended change:

- Add `DiagnosticDescriptor` and `ReportDiagnostic` for unsupported method shapes, missing `IBoltRequest`, unsupported return types, duplicate command names, duplicate routes, and unresolved response contracts.
- Replace syntax-name `Contains` checks with semantic symbol checks against `BoltHandlerAttribute` and `MapEndpointAttribute`.
- Add source-generator unit tests using `CSharpGeneratorDriver`.
- Allow custom-only integration wrappers instead of requiring `[GenerateEndpoints]` entities.

### 3. Fix Bolt Bidirectional Stream Routing

Priority: high

Why it matters: the public API advertises bidirectional streams, but server routing currently does not know which peer sent a stream data frame and always forwards to the recipient.

Evidence:

- Active streams store sender and recipient in `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:29-30`.
- Stream open records both peers in `BoltServer.cs:373-384`.
- `RouteStreamFrameAsync` comments mention direction ambiguity and then forwards only to `peers.Recipient` in `BoltServer.cs:391-406`.

Recommended change:

- Pass the sending `BoltHubConnection` into `RouteStreamFrameAsync`.
- Forward to `peers.Recipient` when the sender is `peers.Sender`; forward to `peers.Sender` when the sender is `peers.Recipient`.
- Add integration tests for service-to-client and client-to-service stream data on the same stream.

### 4. Harden Bolt Frame Limits, Backpressure, and Queues

Priority: high

Why it matters: a high-performance transport needs explicit failure modes under pressure. Several paths either grow buffers without a configured maximum or drop data silently.

Evidence:

- Server receive loop assembles fragmented messages into growing pooled buffers in `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:120-139`.
- Client stream inbound channel is bounded but `EnqueueInbound` uses `TryWrite`, so chunks can be dropped without surfacing an error in `src/Libraries/Bolt/Bolt.Client/BoltStream.cs:40-45` and `BoltStream.cs:151-154`.
- Send queue increments pending sends before enqueue, but `SendSlowAsync` does not clean up the pooled buffer if `WriteAsync` fails before the send loop owns it in `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1162-1179`.

Recommended change:

- Add configurable `MaxFrameBytes` and close connections on protocol violations.
- Decide stream reliability semantics: await capacity, close with a backpressure error, or explicitly expose drop counters.
- Wrap slow enqueue paths with buffer return and pending count decrement on failure.
- Add metrics for queue depth, pending bytes, dropped frames, frame parse errors, and stream close reasons.

### 5. Fix Hybrid Cache Size Handling

Priority: high

Why it matters: memory cache is configured with a size limit by default, but entries are inserted without setting `Size`. In Microsoft.Extensions.Caching.Memory, this can throw at runtime when a size limit is configured.

Evidence:

- Default memory cache size limit is `100` MB in `src/Kernel/XFramework.Core/Services/Caching/CacheOptions.cs:42-47`.
- `AddHybridCaching` maps that to `MemoryCacheOptions.SizeLimit` in `src/Kernel/XFramework.Core/Extensions/CachingExtensions.cs:67-77`.
- `SetInMemoryCache` inserts entries without `.SetSize(...)` in `src/Kernel/XFramework.Core/Services/Caching/HybridCacheService.cs:401-425`.

Recommended change:

- Either remove the default `MemoryCacheSizeLimitMb`, or set a size on every cache entry.
- If accurate object sizing is not practical, use entry count as the unit and document it.
- Add regression tests for `AddHybridCaching` with size limit enabled.

### 6. Decouple Core From IdentityServer Contracts

Priority: high

Why it matters: `XFramework.Core` should be a kernel package, but it references a specific module's domain project. That makes the core framework dependent on a feature module and complicates packaging/reuse.

Evidence:

- `XFramework.Core.csproj` references `IdentityServer.Domain.Shared` in `src/Kernel/XFramework.Core/XFramework.Core.csproj:62-66`.
- `TenantResolver` uses `IdentityServer.Domain.Shared.Contracts.Tenant` in `src/Kernel/XFramework.Core/Services/TenantResolver.cs:1-17`.
- `TenantResolver` is currently a placeholder that throws for actual tenant lookup in `TenantResolver.cs:35-39`.

Recommended change:

- Move a minimal tenant contract into `XFramework.Domain.Shared`, or define `ITenantInfo` in shared abstractions.
- Implement tenant retrieval via an integration wrapper or explicit host-provided resolver.
- Keep IdentityServer-specific entity models out of `XFramework.Core`.

## Architectural Consistency Improvements

### 7. Normalize Module Composition

Priority: medium

Why it matters: modules mix direct `Program.cs` registrations, installer registrations, generated endpoints, and manual endpoints. This increases onboarding cost and creates duplication.

Evidence:

- `XApplication.Configure<T>` automatically calls installers in `src/Kernel/XFramework.Core/Extensions/XApplication.cs:17-31`.
- IdentityServer registers `IAuthService` directly in `Program.cs:13-17` and also in `Installers/ServicesInstaller.cs:13-15`.
- Wallets uses generated endpoints plus manual endpoints in `src/Modules/XFramework.Wallets/Wallets.Api/Program.cs:50-60`.
- Inventario uses generated entity endpoints and generated feature endpoints in `src/Modules/XFramework.Inventario/Inventario.Api/Program.cs:44-48`.
- SmsGateway registers `AppDbContext` and health checks, but `Program.cs` does not call `EnsureDatabase`, unlike IdentityServer, Wallets, Messaging, Community, and Inventario.

Recommended change:

- Pick one registration style as default: installer-owned or `Program.cs`-owned.
- Document exceptions for manual endpoints, especially route/header binding and `IResult` cases.
- Make module startup templates consistent across services.

### 8. Split Large Services Along Feature Boundaries

Priority: medium

Why it matters: VSA endpoints are thin, but some services have become large procedural modules. This weakens testability and change isolation.

Evidence:

- `AuthService` handles credentials, authentication, lockout, session, verification, messaging, password reset, and files. It spans 1416 lines: `src/Modules/XFramework.IdentityServer/IdentityServer.Api/Services/AuthService.cs:1-1416`.
- `AuthService` depends directly on `IMessagingServiceWrapper` and sends SMS/email OTPs in `AuthService.cs:524-599`.
- `WalletOperationsService` spans 1521 lines and includes lifecycle, balance mutation, transfer, hold/release/reversal, freeze/unfreeze, and event publication in `src/Modules/XFramework.Wallets/Wallets.Api/Services/WalletOperationsService.cs:1-1521`.

Recommended change:

- Split IdentityServer into focused services: credentials, authentication/session, verification/password reset, and storage/files.
- Split Wallets into lifecycle, balance mutation, transaction orchestration, queries, and policy/rule validation.
- Keep endpoint methods thin, but let each feature depend on a smaller service surface.

### 9. Move Inventario Contracts Out of Service Implementation

Priority: medium

Why it matters: contracts defined inside service files break the module contract pattern and make generated wrappers/transport compatibility harder.

Evidence:

- Inventario request DTOs and `PaginatedList<T>` are defined at the bottom of `src/Modules/XFramework.Inventario/Inventario.Api/Services/ProductService.cs:310-368`.
- Other modules place request contracts in `{Module}.Domain.Shared/Contracts/Requests` and responses in `{Module}.Domain.Shared/Contracts/Responses`.

Recommended change:

- Move request/response contracts to `Inventario.Domain.Shared`.
- Add `[MemoryPackable]`, `ICommand`/`IQuery`, and `IBoltRequest<TRequest,TResponse>` where remote calls are intended.
- Keep API services dependent on shared contracts, not locally declared DTOs.

### 10. Formalize Payments Module Direction

Priority: medium

Why it matters: Payments does not follow the same module shape as the others. That may be intentional, but it should be explicit.

Evidence:

- Solution includes `Payments.Core` and `Payments.Domain.Shared`, but no `Payments.Api` in `XFramework.slnx:38-41`.
- `PaymentGatewayService` returns `PaymentResponse` directly rather than `Result<T>` in `src/Modules/XFramework.Payments/Payments.Core/Services/PaymentGatewayService.cs:22-33`.

Recommended change:

- Decide whether Payments is a library-only component or a first-class service module.
- If first-class, add `Payments.Api` with VSA endpoints, shared requests/responses, validators, and generated HTTP/Bolt handlers.
- If library-only, document that exception in the architecture docs.

### 11. Reduce Domain.Shared Cross-Module Coupling

Priority: medium

Why it matters: shared contracts should remain stable boundaries. Referencing another module's Domain.Shared from a module Domain.Shared can leak entities and create dependency chains.

Evidence:

- `Wallets.Domain.Shared` references `IdentityServer.Domain.Shared` in `src/Modules/XFramework.Wallets/Wallets.Domain.Shared/Wallets.Domain.Shared.csproj:19-21`.
- `Messaging.Domain.Shared` references `IdentityServer.Domain.Shared` in `src/Modules/XFramework.Messaging/Messaging.Domain.Shared/Messaging.Domain.Shared.csproj:19-21`.
- `Community.Domain.Shared` references `IdentityServer.Domain.Shared` in `src/Modules/XFramework.Community/Community.Domain.Shared/Community.Domain.Shared.csproj:21-23`.

Recommended change:

- Prefer IDs, small read models, or integration contracts over sharing another module's entity/domain project.
- Keep module Domain.Shared projects focused on their own contracts and stable cross-service messages.

## Persistence and Runtime Improvements

### 12. Make EF Configuration Discovery Deterministic

Priority: medium

Why it matters: `AppDbContext` relies on currently loaded assemblies. Tests already need to force-load modules to make model configuration discovery work.

Evidence:

- `AppDbContext` scans `AppDomain.CurrentDomain.GetAssemblies()` and filters names in `src/Kernel/XFramework.Domain/Contexts/AppDbContext.cs:63-72`.
- Integration tests force-load a module Domain.Shared type so configurations are discovered in `src/Tests/IdentityServer.IntegrationTests/Infrastructure/IntegrationTestFixture.cs:263-279`.

Recommended change:

- Register module configuration assemblies explicitly per service.
- Or generate a deterministic list of configuration assemblies at compile time.
- Add a startup validation check that expected entity configurations are loaded.

### 13. Review Global Query Filter Behavior for Auth and System Flows

Priority: medium

Why it matters: unauthenticated flows fall back to a default tenant or `Guid.Empty`. That is simple, but it can hide tenant context bugs or exclude all rows unexpectedly.

Evidence:

- Global filters apply soft delete and tenant filters to all matching entities in `src/Kernel/XFramework.Domain/Contexts/XDbContext.cs:58-101`.
- Tenant resolution falls back to `Tenant:DefaultId` or `Guid.Empty` in `XDbContext.cs:104-136`.
- Saves throw if `TenantId` is empty in `XDbContext.cs:191-197`.

Recommended change:

- Make tenant context explicit for unauthenticated endpoints that legitimately need a default tenant.
- Add tests for auth/register/verification flows with missing tenant configuration.
- Consider a scoped `ITenantContext` abstraction rather than reading claims/config directly inside the DbContext.

### 14. Unify Logging Strategy

Priority: medium

Why it matters: Serilog and ZLogger infrastructure coexist. That may be useful for experiments, but it increases operational complexity if both are active patterns.

Evidence:

- Core configures Serilog globally in `src/Kernel/XFramework.Core/Extensions/XApplication.cs:17-21` and `InstallerExtensions.cs:93-104`.
- Infrastructure also contains a custom ZLogger Seq sink in `src/Infrastructure/XFramework.Integration/Logging/ZLoggerSeqSink.cs:12-35`.

Recommended change:

- Choose the primary logging pipeline.
- Keep the other as an explicitly documented experimental or benchmark-only option.
- Standardize enrichment fields and sampling behavior across services.

## Test and Delivery Improvements

### 15. Add Source Generator Contract Tests

Priority: high

Why it matters: the source generators define the public architecture. Regressions can silently remove endpoints or wrappers.

Evidence:

- Tests exist for caching, data context expression serialization, Bolt, and integration flows, but no source-generator tests were found under `src/Tests`.
- `BoltHandlerGenerator` is a classic `ISourceGenerator` in `src/SourceGenerators/XFramework.SourceGenerators/BoltHandlerGenerator.cs:21`, while other generators use incremental patterns.

Recommended change:

- Add tests that compile sample endpoint code and assert generated REST adapter, Bolt handler, handler registry, and wrapper output.
- Add tests for invalid decorated methods and expected diagnostics.
- Add tests for command endpoints returning `Task<Result>` and query endpoints returning `Task<Result<T>>`.

### 16. Remove False-Green Release Paths

Priority: high

Why it matters: packages can publish even when tests, pack, or push fail.

Evidence:

- Unit test failures are tolerated in `.github/workflows/publish.yml:44-46`.
- Pack step uses `|| true` in `.github/workflows/publish.yml:48-55`.
- NuGet push uses `|| true` in `.github/workflows/publish.yml:60-65`.

Recommended change:

- Make test and pack failures block publishing.
- Keep `--skip-duplicate`, but do not swallow other push failures.
- Split CI validation from release publishing.

### 17. Resolve Bolt Test/Runtime Mismatch Comments

Priority: medium

Why it matters: comments indicate known transport timeout risks around thin-protocol handlers. The code may have moved on, but the comments should be reconciled and verified.

Evidence:

- `src/Tests/XFramework.TestInfrastructure/BoltTestHelper.cs:64-66` says service apps still use SignalR for handler registration until a source-generator update.
- `src/Tests/IdentityServer.IntegrationTests/Infrastructure/IntegrationTestFixture.cs:195-198` has the same note.
- Active infrastructure uses `BoltHandlerRegistrationHostedService` and scans generated `IBoltHandler` in `src/Infrastructure/XFramework.Integration/Extensions/ServiceCollectionExtensions.cs:76-128`.

Recommended change:

- Verify the current integration tests against actual thin-protocol handler generation.
- Remove stale comments if no longer true.
- If still true, prioritize end-to-end Bolt handler tests and generator fixes.

### 18. Harden Docker Runtime Images

Priority: medium

Why it matters: the runtime image runs as root and installs tools into the final image.

Evidence:

- Runtime image installs `curl` and has no explicit non-root user in `Dockerfile:24-36`.

Recommended change:

- Run containers as a non-root user.
- Prefer health checks that do not require adding package managers/tools to the runtime image, or use a minimal trusted base with required tools.
- Move default passwords out of compose defaults.

## Suggested Roadmap

### Immediate

- Remove and rotate committed secrets.
- Fix CI false-green paths.
- Add source-generator diagnostics for invalid decorated methods.
- Fix the hybrid cache size-limit issue.

### Next

- Fix Bolt bidirectional stream routing and backpressure behavior.
- Add source-generator contract tests.
- Decouple `XFramework.Core` from `IdentityServer.Domain.Shared`.
- Normalize module service registration ownership.

### Later

- Split `AuthService` and `WalletOperationsService` into smaller feature services.
- Move Inventario contracts to `Inventario.Domain.Shared`.
- Make EF configuration discovery deterministic.
- Clarify whether Payments is a service module or a library-only component.
- Add Bolt-specific OpenTelemetry metrics and traces.

## Notes

- I did not run a full validation build as part of this report because there was an active parallel task in another terminal and a known unrelated Blazor build issue was already observed earlier.
- Findings are based on static code review and targeted scans, not runtime profiling.
