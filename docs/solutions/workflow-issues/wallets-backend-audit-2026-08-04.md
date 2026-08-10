---
title: "Wallets Backend Audit Revalidation"
date: 2026-08-09
category: workflow-issues
module: Wallets
problem_type: security_and_backend_compliance
component: wallets
severity: medium
status: open
tags: [wallets, security, bolt, identityserver, ledger, ef-core, caching, testing]
---

# Wallets Backend Audit Revalidation

## Audit Scope

- Baseline: `origin/develop` at `eef4105429522174fd3a72af4beee5448ec6256d`, including generated authorization parity from PR #403.
- Authority: `CLAUDE.md`, `rules/BackendGuidelines.md`, and the canonical VSA, EF Core, caching, trusted-invocation, remote `IDataContext`, and wrapper-test documents they require.
- Method: a fresh post-IdentityServer/post-Bolt review using the `xframework-audit-module` workflow, followed by remediation and regression testing on `codex/wallets-backend-post-bolt-audit`.
- Areas: financial authorization, actor/service/tenant propagation, Bolt and REST handlers, background/admin work, service and transport token reuse, VSA, services, EF/schema ownership, migrations, caching, validation, packages, generated reads, remote `IDataContext`, wrappers, and tests.

## Current Result

**Overall grade: C+.** No Critical or High finding remains after the Wallets remediation and the shared generated-authorization rollout. The remaining Medium findings still require scale, deployment, ownership, caching, and contract work before the module should be graded production-complete.

| Severity | Count |
|---|---:|
| Critical | 0 |
| High | 0 |
| Medium | 8 |
| Low | 3 |

## Critical Findings

No current Critical finding remains after this remediation pass.

## High Findings

No current High finding remains after this revalidation.

## Medium Findings

### M1. Wallets applies migrations during application startup

`src/Modules/XFramework.Wallets/Wallets.Api/Program.cs:96` calls `EnsureDatabase<AppDbContext>()`, which can run migrations from each service replica.

**Impact:** replicas can race schema changes and bypass the migration-runner deployment authority.

**Missing coverage:** production composition should prove Wallets cannot migrate at runtime.

### M2. Reporting and reconciliation retain unbounded and N+1 query paths

Statements materialize all matching ledger entries (`WalletWorkflowService.cs:1404-1440`), settlement reporting loads deposit and withdrawal sets before combining/paging (`:1755-1845`), and reconciliation queries snapshot/ledger state per wallet (`WalletReconciliationService.cs:42-79`).

**Impact:** large tenants can cause high memory use, excessive commands, and slow financial reports.

**Missing coverage:** add mandatory paging, large-volume query-shape tests, and reconciliation command-count tests.

### M3. Multi-destination outbox delivery is message-level

`WalletOutboxPublisher` sends destinations sequentially and throws on the first failure (`src/Modules/XFramework.Wallets/Wallets.Api/Services/IWalletOutboxService.cs:49-86`). Retrying the message resends to destinations that already succeeded.

**Impact:** downstream consumers can receive duplicates, and provider response bodies can enter internal diagnostics.

**Missing coverage:** add destination-level attempts, partial-failure tests, and error redaction.

### M4. Payments integration and Finance mappings remain undocumented ownership exceptions

Wallets directly references `Payments.Core` (`src/Modules/XFramework.Wallets/Wallets.Api/Wallets.Api.csproj:18-19`) and uses provider services directly (`Services/WalletWorkflowService.cs:5`). Currency/exchange-rate mappings also use the shared `Finance` schema.

**Impact:** cross-module business behavior and schema ownership are coupled without an approved architecture exception.

**Missing coverage:** add an architecture decision and dependency test, or move provider behavior behind an approved contract.

### M5. Advanced features remain concentrated outside VSA slices

`Features/AdvancedWallets/Endpoint.cs` contains 274 lines of unrelated workflows, while `WalletWorkflowService.cs` is 2,806 lines. Boundary validators now cover the principal advanced mutation requests (`Features/AdvancedWallets/Validators.cs`), but ownership remains broad.

**Impact:** authorization, validation, and transaction changes have a large review and regression surface.

**Missing coverage:** no architecture guard limits multi-capability endpoint/service concentration.

### M6. Generated cache declarations remain decorative

Wallet entities declare `CacheDurationSeconds` and `CacheKeyPrefix`, for example `Wallet.cs:13-14`, but generated reads do not apply canonical cache keys, TTLs, or mutation invalidation.

**Impact:** the contracts imply caching that does not occur, while reference and financial reads continue to hit PostgreSQL.

**Missing coverage:** no cache hit, tenant-key, fail-open, TTL, or invalidation tests exist.

### M7. OpenAPI remains partial and coupled to persistence contracts

Wallets filters the OpenAPI document to selected DTO-backed routes (`src/Modules/XFramework.Wallets/Wallets.Api/Program.cs:26-43`) because generated EF entity schemas contain navigation cycles.

**Impact:** consumers cannot rely on a complete Wallets API contract.

**Missing coverage:** no complete document/schema test covers all public routes without EF navigation types.

### M8. Integration tests do not run production IdentityServer validation

The fixture exercises Bolt transport, trusted actor/service envelopes, wrapper calls, PostgreSQL, and migrations, but replaces IdentityServer validation with test providers (`src/Tests/Wallets.IntegrationTests/Infrastructure/WalletsTestFixture.cs:37-130,260-298`). Static contract filters also start Testcontainers because the assembly-wide fixture is unconditional (`:37,101-110`).

**Impact:** wrapper tests do not prove revoked-session, production audience/scope, or live capability-resolution behavior, and static-only tests still require Docker.

**Missing coverage:** add production-composition security tests and separate Docker-free contract tests from the database fixture.

## Low Findings

### L1. `Microsoft.EntityFrameworkCore.InMemory` remains unused

`src/Modules/XFramework.Wallets/Wallets.Api/Wallets.Api.csproj:27-33` references the provider although Wallets production and integration paths use PostgreSQL.

### L2. Generated nullability warnings remain noisy

Build output still contains substantial warnings from generated contracts/change trackers, reducing the signal of new nullability defects.

### L3. Remote mutation fail-closed behavior lacks a Wallets runtime test

Wallets financial entities remain read-only at the generated contract, but `src/Tests/Wallets.IntegrationTests/Tests/DataContextTests.cs` proves only a query.

**Missing coverage:** attempt remote create/update/delete for a financial entity and assert rejection.

## Resolved Or No Longer Applicable Findings

1. **Generated authorization is now consistent across REST, generated services/Bolt wrappers, and remote `IDataContext`.** PR #403 introduced a server-owned generated policy registry and centralized enforcement. Wallets declares exact capability, feature, endpoint-type, and route metadata for every remotely exposed entity and verifies the map with a completeness guard (`GeneratedEntityAuthorizationCompletenessTests.cs:16`). Tenant-wide generated `Wallet` and `WalletAddress` reads require `wallets.reporting:view` (`Wallet.cs:12`; `WalletAddress.cs:12`), while owner-facing custom reads retain their ownership checks. Runtime coverage proves `wallets:view` cannot access generated tenant-wide wallet reads through the wrapper or remote query (`DataContextTests.cs:77`).
2. **Cumulative refund settlement and case decisions are transactionally guarded.** `WalletLedgerExecutionRequest` exposes a transaction-scoped validation callback (`IWalletLedgerService.cs:24`), which executes after deterministic wallet locking and before policy evaluation or postings (`WalletLedgerService.cs:124-140`). Approval locks and reloads the case and original operation, then computes completed refunds from ledger entries inside the serializable ledger transaction; rejection uses an atomic conditional transition so it cannot overwrite a completed settlement (`WalletWorkflowService.cs:1222-1415`). PostgreSQL-backed tests cover competing cumulative refunds, approve-versus-reject consistency, and rejected-operation replay without postings or outbox messages (`WalletAdvancedSystemTests.cs:1264-1530`).
3. **Wallet event leakage is fixed.** The publisher requires tenant scope, and event retrieval resolves trusted tenant/actor context and limits ordinary actors to their own credential. Two-tenant regression coverage is included.
4. **Owner-created arbitrary refunds are blocked.** Resolution requires policy management, an independent decider, a completed original operation, optional transaction linkage, and remaining-refundable-amount validation.
5. **Concurrent withdrawal approval no longer duplicates provider payout.** Approval atomically claims `Approved -> Settling`; only the winner invokes the provider, while hold replay reloads current workflow state.
6. **The public webhook route is HMAC-only again.** The HTTP endpoint is explicitly anonymous and tenantless; the internal Bolt path is separate and requires Portal, `wallets.admin`, `tenant.target`, and service-target tenant context.
7. **Invalid signatures cannot select an audit tenant.** Rejected events are written only when the provider is configured to a tenant, and raw payloads are capped at 256 KiB.
8. **Wallet administrative handlers now require canonical IdentityServer capabilities.** Values follow `{module}[.{subfeature}]:{view|create|update|delete|manage}`. Policy, outbox, reconciliation, approvals, workflow administration, reports, and core money handlers declare explicit capabilities; sensitive services also enforce them internally.
9. **Deposit and withdrawal creation is durably idempotent.** Tenant-scoped unique keys and request hashes replay identical requests and reject changed payloads. Migration `20260805170822_WalletWorkflowCreateIdempotency` contains only the four intended columns and two indexes.
10. **Batch requests are externally idempotent.** A top-level key is required; all-or-nothing mode replays the batch operation and partial mode derives stable per-item keys.
11. **Rejected financial attempts are persisted and replay faithfully.** Ledger policy/approval/transactional validation rejection creates a rejected operation with request hash, decision metadata, and original HTTP failure status, without postings or outbox publication. Migration `20260809092233_AddWalletOperationFailureStatusCode` adds only the nullable replay-status column.
12. **Manual wallet reads are owner/admin scoped.** Wallet-by-ID and credential queries use trusted actor context instead of client authority.
13. **Advanced mutation validators now cover workflows, webhooks, cases, and batch requests.** Validation remains at the generated endpoint boundary and is backed by integration tests.
14. **Service and Bolt transport token acquisition remains cached and burst-controlled.** Independent singleton providers use per-key caches, refresh skew, single-flight acquisition, timeout, and failure backoff.
15. **Generated financial mutations remain sealed.** Wallet financial entities expose read actions only and are not allowlisted for remote mutation.
16. **Ledger and outbox safety remains intact.** Financial execution uses serializable transactions, deterministic wallet locks, balanced postings, snapshots, tenant-scoped request-hash replay, and atomic outbox creation.

## Verification Evidence

- `dotnet build src/Modules/XFramework.Wallets/Wallets.Api/Wallets.Api.csproj -m:1 /nr:false -v:minimal`: passed with 0 warnings and 0 errors.
- `dotnet build src/Tests/Wallets.IntegrationTests/Wallets.IntegrationTests.csproj -m:1 /nr:false -v:minimal`: passed with 0 warnings and 0 errors.
- `dotnet test src/Tests/Wallets.IntegrationTests/Wallets.IntegrationTests.csproj -m:1 /nr:false`: passed 111/111 against PostgreSQL through a loopback-only SSH/socat tunnel to the `xeon-dev` Docker context.
- Focused generated-authorization, cumulative-refund, case-decision, and rejected-replay tests: passed 5/5.
- `git diff --check`: passed.
- EF migrations were generated with EF Core tooling and applied successfully by the integration fixture. The latest migration adds only `WalletOperation.FailureStatusCode` for durable response replay.
- `dotnet ef migrations has-pending-model-changes` through `XFramework.MigrationRunner`: passed with no pending model changes.
- No service was deployed or restarted.

## Audit Maintenance

Update this document whenever a confirmed finding is fixed, a Wallets security/ledger/workflow contract changes, or shared IdentityServer/Bolt behavior changes a conclusion. Mark a finding resolved only when implementation and regression/integration coverage are both present.
