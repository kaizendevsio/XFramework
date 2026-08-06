---
title: "Communications Backend Audit Revalidation"
date: 2026-08-06
category: workflow-issues
module: Communications
problem_type: backend_compliance
component: communications
severity: critical
status: in_progress
tags: [communications, identityserver, bolt, ef-core, caching, testing, audit]
---

# Communications Backend Audit Revalidation

## Audit Scope

- Current baseline: `origin/develop` at `08a13824b452f8f9645af69abd7bb4e6877378a9`.
- IdentityServer security baseline: `c0e0a47e79c141fe6b40b24a79f353bd200b2629` (PR #398). Later commits through the current baseline do not modify the shared invocation-security implementation.
- Previous audit baseline: `11ba2abbe1d3ba74dab29b312e02fdaea134a465`.
- Revalidation reason: IdentityServer and the shared service-invocation infrastructure now use independently quota-controlled service access tokens and Bolt transport tokens.
- Authority: `CLAUDE.md`, `rules/BackendGuidelines.md`, and the canonical VSA, EF Core, caching, Bolt, wrapper, and integration-test documents they reference.
- Method: fresh review using the `xframework-audit-module` workflow. Previous findings were treated as stale until reverified.
- Scope inspected: Communications API, Domain.Shared, Integration, Tests, Portal consumers, Storage and Notifications contracts, Bolt Hub topic authorization, shared trusted-invocation/token infrastructure, Docker service identities, migrations, packages, and CI.

## Overall Result

**Score: D**

The IdentityServer work resolves the former shared-secret trust model and substantially improves service-token validation, caller binding, actor separation, responder authorization, and token reuse. Communications is still not production-ready. One critical persistence-correctness defect remains, and the post-IdentityServer policy model exposes three newly concrete blockers: selected-tenant Portal administration is rejected by default handler policies, the advertised .NET chat facade does not propagate its actor token to RPC calls and has an invalid DI lifetime, and Communications cannot request the Storage scope required by its attachment validation path in the deployed configuration.

## Remediation Status

The audit above records the `origin/develop` baseline. The first remediation branch resolves the critical correctness and post-IdentityServer invocation blockers without mixing in the larger schema and delivery-pipeline redesigns:

- **Resolved in this branch: C-1.** Communications write paths now reject failed `DataContextResult` commits, preserve the failure status, and return stable public errors rather than provider details.
- **Resolved in this branch: H-1.** Chat handlers require `communications.chat`; Portal admin/settings/moderation handlers require `communications.admin`, allowlist `XFramework.Portal`, use delegated tenant access, and require `identity.tenants:manage` only for cross-tenant targeting. The generator now exposes `RequiredCrossTenantActorCapabilities` to Bolt and HTTP policies.
- **Resolved in this branch: H-2.** `ICommunicationsChatClient` is scoped and actor-backed sessions push the current actor token through `IActorAccessTokenScope` for RPC, transient publish, and subscription calls.
- **Resolved in this branch: H-3.** Communications receives `storage.read` in its deployed default scopes, and Portal receives the Communications admin/chat scopes registered by IdentityServer.
- **Resolved in this branch: M-3 and M-4.** Thread-service failures no longer expose raw exception messages, and wrapper cancellation tokens now reach the Bolt transport.
- **Still open:** H-4 through H-8, M-1, M-2, M-5 through M-7, and the low-severity structural/naming/configuration debt. These require separate IdentityServer Registry contracts, database projection work, distributed state, durable delivery recovery, schema changes, and PostgreSQL-backed CI.

## Critical Findings

### C-1. Twenty-eight write paths ignore failed `IDataContext.SaveChangesAsync` results

`ServerDataContext.SaveChangesAsync` catches concurrency and database-update exceptions and returns a failed `DataContextResult` instead of throwing (`src/Kernel/XFramework.Core/DataContext/ServerDataContext.cs:24-40`). Communications still awaits and discards that result in 28 locations:

- `ThreadService.cs`: `140`, `568`, `635`, `681`, `727`, `866`, `941`, `1054`, `1170`, `1254`, `1467`, `1560`, `1770`, `1802`, `1878`, `1972`, `2061`, `2137`, `2213`, `2265`, `2380`, `2553`, `2649`, `2737`, and `2855`.
- `CommunicationsService.cs`: `186`, `197`, and `313`.

Impact: thread, membership, message, reaction, read-state, attachment, outbox, and direct-delivery methods can return success even though PostgreSQL rejected the write. A committed business object and its outbox record can also diverge.

Missing coverage: `Communications.Tests/Infrastructure/InMemoryDataContext.cs:68-69` always returns success. There is no failed-save regression fixture or PostgreSQL constraint/concurrency test covering these paths.

## High Findings

### H-1. Communications handler policies reject delegated tenant administration and do not enforce module scopes or caller allowlists

Admin reads, moderation, settings, and templates use bare `[BoltHandler]` declarations (`Features/Admin/Endpoint.cs:8-83`, `Features/Admin/ModerationEndpoint.cs:8-70`, `Features/Settings/Endpoint.cs:8-31`, and `Features/Templates/Endpoint.cs:8-96`). The default policy requires an actor and uses `ActorTenant` (`BoltHandlerAttribute.cs:32-39`; `InvocationAuthorizationPolicy.cs:19-27`). `ActorTenant` rejects a requested tenant that differs from the actor tenant (`TrustedInvocationResolver.cs:113-118`). Portal intentionally sends its selected tenant as `RequestedTenantId` (`CommunicationsPortalReadService.cs:150-158`; `CommunicationsPortalSettingsService.cs:58-66`).

The same endpoints do not require the defined `communications.admin` scope, do not allowlist `XFramework.Portal`, and do not require an actor capability. `ResolveAdminAsync` accepts any actor carrying the `Admin` or `SuperAdmin` role before checking the calling service (`CommunicationsRequestContextResolver.cs:109-124`). Chat handlers such as message and thread creation are also bare (`Features/Messages/CreateMessage/Endpoint.cs:8-20`; `Features/Threads/Create/Endpoint.cs:8-20`) and therefore do not enforce the defined `communications.chat` scope (`XFrameworkServiceScopes.cs:11-13`). Portal's registered and default scopes currently omit both Communications scopes (`docker-compose.yml:206-213`, `610-620`).

Impact: cross-tenant ControlPanel administration fails before Communications service logic even for a valid super-admin, while same-tenant admin/chat calls rely on generic service identity plus actor role rather than least-privilege module authorization. Any service allowed to obtain a token for Communications can attempt these actor-delegated handlers because no caller allowlist is present.

Missing coverage: generated-handler tests stub authorization or exercise only the default policy (`GeneratedBoltHandlerAuthorizationTests.cs:38-80`, `289-317`). There are no end-to-end tests for selected-tenant delegation, wrong service caller, missing `communications.admin`, missing `communications.chat`, or required cross-tenant actor capability.

### H-2. `ICommunicationsChatClient` cannot reliably perform authenticated RPC and has a captive scoped dependency

`ICommunicationsChatClient` is registered as a singleton while `ICommunicationsServiceWrapper` is scoped (`CommunicationsServiceDriver.cs:976-983`), and the singleton constructor directly captures that wrapper (`CommunicationsChatClient.cs:145-148`). `ForCurrentActorAsync` obtains and stores an actor access token (`CommunicationsChatClient.cs:167-190`), but session RPC methods call the wrapper directly (`CommunicationsChatClient.cs:218-230`) without pushing that token into `IActorAccessTokenScope`. `BoltDriver` obtains the actor token only from its injected ambient provider (`BoltDriver.cs:381-387`). The session token provider is used only by realtime subscription helpers (`CommunicationsChatClient.cs:430-478`); publishing typing/presence again uses the unaffiliated wrapper path (`452-463`). The default chat actor provider always returns `null` (`CommunicationsChatClient.cs:25-33`).

Impact: resolving the singleton can fail scope validation or capture a root scoped wrapper. Even when an application supplies `ICommunicationsChatActorProvider`, `ForCurrentActorAsync` RPC calls can arrive without actor credentials and fail with 401. The facade advertised as the supported .NET/Blazor chat integration path is therefore not self-contained or reliable.

Missing coverage: `CommunicationsWrapperCoverageTests.cs:25-39` checks method-name parity only. There is no DI validation test or chat-client workflow test proving actor-token propagation for RPC, transient publish, and realtime subscribe.

### H-3. Deployed Communications cannot request the Storage scope required for attachment validation

Attachment creation calls `ValidateStorageFileReference` (`ThreadService.cs:2322`). Storage correctly requires `storage.read` (`Storage.Api/Features/Files/ValidateReference/Endpoint.cs:6-16`). `BoltDriver` asks `IServiceTokenProvider` for the target audience with `scopes: null` (`BoltDriver.cs:381-387`), which resolves to the caller's configured defaults. IdentityServer allows Communications to request Storage scopes (`docker-compose.yml:222-229`), but the Communications service defaults include only `bolt.service`, `identity.session.validate`, `notifications.send`, and `tenant.target` (`docker-compose.yml:332-340`).

Impact: attachment-reference validation receives a service token without `storage.read`, so valid attachment linking fails with 403 in the deployed composition.

Missing coverage: no integration test runs a real Communications-to-Storage wrapper call with issued service credentials and asserts scope/audience behavior.

### H-4. Communications writes IdentityServer-owned Registry tables directly

`CommunicationsSettingsService` creates and updates `RegistryConfiguration` and `RegistryConfigurationGroup` through the shared `IDataContext` (`CommunicationsSettingsService.cs:68-121`, `172-193`, `249-279`). Policy, legacy-template backfill, and SMS-agent lookup also query Registry entities directly (`ICommunicationsPolicyService.cs:49-52`, `CommunicationsTemplateService.cs:466-474`, `CommunicationsService.cs:284-288`). The settings endpoint documents that it persists Registry rows (`Features/Settings/Endpoint.cs:21-31`).

Impact: Communications bypasses IdentityServer ownership, authorization, validation, cache invalidation, and audit behavior. This violates the single-database schema-per-module boundary even though the physical database is shared.

Missing coverage: no IdentityServer wrapper contract proves settings create/update, tenant isolation, cache invalidation, or legacy-template migration through the owning module.

### H-5. Admin list projections truncate data before filtering, sorting, and paging

`QueryUsersAsync` and `QueryThreadsAsync` build in-memory row collections before applying the grid request (`CommunicationsAdminReadService.cs:27-29`, `153-165`). Builders first cap memberships, messages, threads, invites, blocks, pins, and reports (`378-418`, `433-483`). Search, filters, sorting, totals, and paging run only after materialization (`932-948`).

Impact: tenants beyond any cap receive incomplete rows, incorrect totals, and false-negative searches. Memory and CPU grow before pagination.

Missing coverage: admin-read tests use small in-memory datasets and do not verify Npgsql translation, records beyond caps, accurate totals, or stable database paging.

### H-6. Action rate limits are process-local

`CommunicationsActionRateLimiter` stores one-minute counters in a local `ConcurrentDictionary` (`ICommunicationsActionRateLimiter.cs:25-93`) and is registered as a singleton (`Program.cs:22-31`).

Impact: each replica has independent limits and restarts clear all counters. Message, reaction, invite, attachment, report, and external-delivery limits scale with replica count and are bypassable across restarts.

Missing coverage: no distributed-store, multi-replica, atomic-window, degradation-policy, or restart test exists.

### H-7. Communications has no PostgreSQL-backed service integration suite or dedicated CI gate

`MessageModelConfigurationTests` builds an Npgsql model but never opens a database (`Communications.Tests/Domain/MessageModelConfigurationTests.cs:53-69`). Service tests use custom in-memory contexts. `publish.yml` builds the solution but runs only `XFramework.Core.Tests` (`.github/workflows/publish.yml:41-45`); there is no Communications integration workflow or integration-test project.

Impact: migrations, constraints, filtered unique indexes, query filters, Npgsql translation, transactions, outbox leasing, concurrent direct-thread creation, and cross-module service-token behavior can regress without CI failure.

Missing coverage: Testcontainers/PostgreSQL migration and workflow tests, real Bolt wrapper calls, attachment authorization, duplicate races, and deployment migration-runner smoke tests are absent.

### H-8. External direct delivery remains idempotent but is not durably dispatched

`CommunicationsService` claims and commits a `MessageDirect`, then synchronously invokes Notifications (`CommunicationsService.cs:119-178`). A process crash after the claim commit and before/during the wrapper call leaves the record in `Processing`; recovery depends on the original caller retrying the same request after the two-minute lease. Only the chat outbox dispatcher is registered (`Installers/HostedServiceInstaller.cs:6-11`). Failure-status saves also ignore `DataContextResult` (`CommunicationsService.cs:180-197`).

Impact: an accepted direct-delivery request can remain stranded indefinitely without an automatic recovery worker, despite request-level idempotency.

Missing coverage: direct-delivery tests cover retries using in-memory contexts, not process death, PostgreSQL uniqueness, automatic lease recovery, or a durable Notifications handoff worker.

## Medium Findings

### M-1. Tenant policy caching is not replica-coherent

`CommunicationsPolicyService` uses a five-minute `IMemoryCache` entry (`ICommunicationsPolicyService.cs:34-80`). A settings write invalidates only the current process (`CommunicationsSettingsService.cs:113-123`).

Impact: replicas can enforce different edit windows, attachment rules, feature switches, moderation settings, and rate limits for up to five minutes.

Missing coverage: no distributed invalidation, policy version, pub/sub invalidation, or multi-instance test exists.

### M-2. Configured retention is not executed

`SoftDeletedMessageRetentionDays` is loaded into the policy (`ICommunicationsPolicyService.cs:67-77`), but only the outbox dispatcher is registered (`Installers/HostedServiceInstaller.cs:6-11`).

Impact: soft-deleted messages, files, reactions, expired invites, delivery records, stale outbox history, and moderation audit rows grow indefinitely.

Missing coverage: no retention cutoff, tenant policy, batch cleanup, legal-hold, or PostgreSQL cleanup tests exist.

### M-3. Thread APIs disclose raw exception details

Thirty-two `ThreadService` catch paths return interpolated `ex.Message`, including thread creation (`ThreadService.cs:150`), message creation (`1477`), search (`1709`), and attachment retrieval (`2476`).

Impact: SQL, provider, constraint, storage, and implementation details can be returned over HTTP and Bolt.

Missing coverage: no test asserts stable public errors plus structured server-side logging for unexpected exceptions.

### M-4. Wrapper cancellation tokens are checked and then discarded

Wrapper methods call `ct.ThrowIfCancellationRequested()` and then invoke `SendAsync`/`SendVoidAsync` without passing `ct`; representative calls are `CommunicationsServiceDriver.cs:306-340`. `DriverBase` supports transport cancellation (`DriverBase.cs:23-38`).

Impact: requests continue consuming Bolt and service resources after client disconnect, deadline expiry, or host shutdown.

Missing coverage: wrapper tests do not cancel after dispatch and assert transport cancellation.

### M-5. `ConcurrencyStamp` is not configured as an EF concurrency token

Communications services rotate `ConcurrencyStamp`, but a module-wide search finds no `IsConcurrencyToken`, concurrency-check attribute, or row-version mapping; representative mutable configuration is `MessageConfiguration.cs:7-40`.

Impact: concurrent message edits, moderation actions, settings updates, membership changes, and delivery transitions are last-write-wins rather than producing controlled conflicts.

Missing coverage: no PostgreSQL `DbUpdateConcurrencyException` regression test exists.

### M-6. Runtime migration remains reachable from the service

Communications calls `app.EnsureDatabase<AppDbContext>()` at startup (`Program.cs:57-58`), and the helper invokes `Database.Migrate()` when enabled (`src/Kernel/XFramework.Core/Extensions/XApplication.cs:63-82`).

Impact: a missing or incorrect deployment setting can let an application replica race the migration runner or alter the shared database at startup.

Missing coverage: no production-composition test proves runtime migration is disabled and the migration runner is the only schema authority.

### M-7. Moderation queries are unbounded and unknown actions silently mutate state

Rule evaluation and rule listing use unbounded `ToListAsync` (`ICommunicationsModerationService.cs:50-65`, `68-89`), report audit history is unbounded (`281-300`), and an unknown action normalizes to `Reviewed` (`369-378`).

Impact: large rule/audit collections produce unbounded work and malformed admin commands can unexpectedly advance a report.

Missing coverage: pagination limits, invalid action rejection, oversized notes/patterns, regex timeout/safety, and large rule-set tests are absent.

## Low Findings

### L-1. VSA slices and shared services remain oversized

`Features/Platform/Endpoint.cs` contains many endpoint classes across 245 lines. `ThreadService.cs` is 3,344 lines, `CommunicationsAdminReadService.cs` is 1,207 lines, and `CommunicationsTemplateService.cs` is 1,026 lines.

Impact: feature ownership, targeted review, and focused regression testing are harder than the canonical one-entry-point-per-slice structure.

### L-2. Database naming debt remains in the active model

The `ReceivedAt` property maps to the misspelled physical column `RecievedAt` (`MessageDirectConfiguration.cs:27`).

Impact: migrations and operational SQL continue carrying the incorrect name.

### L-3. Communications documentation and configuration describe stale behavior

The module guide still says trusted internal calls use shared-secret signed metadata (`src/Modules/XFramework.Communications/AGENTS.md:21-25`), while production now uses IdentityServer-issued asymmetric service tokens and separate actor tokens. The guide says startup migrations must not run (`AGENTS.md:52`) while `Program.cs:57-58` still enables that path. OpenAPI metadata identifies the service as Identity Server (`appsettings.json:2-7`; `appsettings.Development.json:5-10`), and development configuration retains `MaxPoolSize=900` (`appsettings.Development.json:20-22`).

Impact: future agents can reintroduce obsolete trust patterns, generated documentation is misleading, and development databases can be overcommitted.

## Post-IdentityServer Security Revalidation

### Resolved or no longer applicable

1. **Shared-secret metadata trust and caller-name spoofing are resolved.** `RequestMetadata` now contains only ordered audit/request context (`RequestMetadata.cs:4-23`). Communications has no production `RequestMetadataTrust` or `TrustedMetadata` usage.
2. **Service and actor identity are separate and centrally validated.** `TrustedInvocationResolver` validates the service token, required scopes/caller, actor token, and effective tenant independently (`TrustedInvocationResolver.cs:11-77`, `91-169`). `CommunicationsRequestContextResolver` consumes only the trusted invocation context (`CommunicationsRequestContextResolver.cs:68-106`, `150-185`).
3. **Service-token audience and Bolt caller provenance are bound.** The authorizer validates against the configured Communications audience and compares token `client_id` to the authenticated sender hash (`BoltServiceInvocationAuthorizer.cs:22-43`).
4. **Wrong Bolt responders can no longer complete another service's RPC.** Pending invocations retain the expected responder (`BoltServer.cs:904-915`), and responses from a different stream are rejected (`997-1017`).
5. **Communications topic authorization is tenant/member aware.** User subscribe/ack is actor-bound, service publish is limited to the authenticated `XFramework.Communications` identity, and thread typing checks membership (`CommunicationsBoltTopicAuthorizer.cs:39-95`, `98-119`, `159-191`).
6. **Service access and Bolt transport tokens are cached independently and reused.** Both providers are singletons (`ServiceCollectionExtensions.cs:95-108`); service tokens are cached by normalized audience/scopes (`IdentityServerServiceTokenProvider.cs:27-43`), transport tokens use a separate cache (`IdentityServerBoltTransportTokenProvider.cs:6-27`), and each cache performs single-flight acquisition with refresh skew and failure backoff (`ServiceIdentityHttpClient.cs:89-170`, `173-209`). Focused tests prove concurrent transport callers issue one HTTP request and normalized service-token requests reuse one token (`IdentityServerTokenProviderTests.cs:51-76`, `183-213`). Communications therefore does not create avoidable token-acquisition bursts under the new independent IdentityServer quotas.
7. **Canonical service naming is correct.** The wrapper targets `XFramework.Communications` (`CommunicationsServiceDriver.cs:291-299`) and Docker registers the same client ID (`docker-compose.yml:222-229`, `332-340`).
8. **Remote `IDataContext` is not a Communications business bypass.** Communications does not register generic remote data-context handlers, and Portal Communications admin/settings/template services use `ICommunicationsServiceWrapper`.
9. **Downstream Notifications handoff has an explicit service policy.** Notifications requires `notifications.send` plus `tenant.target` and allowlists Communications (`Notifications.Api/Features/Notifications/Create/Endpoint.cs:9-23`).
10. **Earlier functional fixes remain present.** Direct-thread uniqueness, default member roles, attachment pagination/detach, reaction request identity checks, privacy-safe admin previews, request-id direct-delivery idempotency, and the leased chat outbox remain implemented.

## What Is Working Well

1. All Communications-owned entity configurations map to the `Communications` schema.
2. HTTP endpoints are authorization-protected and grouped behind tenant module feature gates (`Program.cs:43-66`).
3. Service-token validation, actor validation, effective-tenant resolution, Bolt sender binding, and responder binding are centralized rather than reimplemented in Communications.
4. Committed chat events use a leased/retrying outbox, and realtime topic authorization is tenant/user/thread aware.
5. Portal Communications business reads and writes are wrapper-backed rather than direct remote `IDataContext` mutations.
6. The module targets .NET 10/C# 14 and uses centrally managed package versions.
7. Current package scans report no vulnerable or deprecated Communications API packages.

## Recommended Remediation Order

1. Make every Communications write inspect and propagate `DataContextResult`; add failed-save and PostgreSQL constraint tests.
2. Correct endpoint policies: delegated tenant access plus explicit actor capability and Portal allowlist for admin operations, `communications.admin`/`communications.chat` scopes, and matching Portal client scope configuration.
3. Make `ICommunicationsChatClient` scoped or otherwise lifetime-safe, and push its session actor token through `IActorAccessTokenScope` for every RPC and transient publish.
4. Request `storage.read` explicitly for attachment validation, or include it in Communications defaults with a cross-service integration test.
5. Move Registry behavior behind an IdentityServer-owned wrapper contract.
6. Replace capped in-memory admin projections with database-side projections, filters, counts, sorting, and paging.
7. Replace process-local action limits and policy invalidation with distributed implementations.
8. Add durable Communications-to-Notifications delivery recovery and a PostgreSQL-backed Communications integration project with a required CI workflow.
9. Implement retention, concurrency-token mappings, sanitized ThreadService errors, cancellation propagation, and strict/paginated moderation APIs.
10. Remove runtime migration, then address VSA, physical naming, module guide, OpenAPI, and development pool configuration debt.

## Verification Evidence

- `dotnet test src/Modules/XFramework.Communications/Communications.Tests/Communications.Tests.csproj -c Release --no-restore`: 112 passed, 0 failed, 0 skipped after the first remediation pass.
- `dotnet test src/Tests/XFramework.SourceGenerators.Tests/XFramework.SourceGenerators.Tests.csproj -c Release`: 49 passed, 0 failed, 0 skipped.
- Focused Bolt security/token tests (`IdentityServerTokenProviderTests` and `CommunicationsBoltTopicAuthorizerTests`): 27 passed, 0 failed, 0 skipped.
- `dotnet build src/Modules/XFramework.Communications/Communications.Api/Communications.Api.csproj -c Release --no-restore`: succeeded with 0 warnings and 0 errors.
- `dotnet build src/Presentation/XFramework.Portal/XFramework.Portal.csproj -c Release --no-restore`: succeeded with 0 errors; existing warnings remain outside this remediation scope.
- `dotnet list Communications.Api.csproj package --vulnerable --include-transitive`: no vulnerable packages from configured sources.
- `dotnet list Communications.Api.csproj package --deprecated --include-transitive`: no deprecated packages from configured sources.
- Static checks after the first remediation pass: no ignored Communications persistence results, no raw ThreadService `ex.Message` responses, and no cancellation-aware wrapper method that drops its token. EF concurrency-token mapping and a Communications PostgreSQL integration suite remain absent.
- Audit ran in a clean dedicated worktree from current `origin/develop`; pre-existing user and agent work in other worktrees was not modified.
