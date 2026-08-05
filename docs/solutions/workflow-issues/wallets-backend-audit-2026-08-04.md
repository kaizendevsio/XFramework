---
title: "Wallets Backend Audit Revalidation"
date: 2026-08-06
category: workflow-issues
module: Wallets
problem_type: security_and_backend_compliance
component: wallets
severity: high
status: open
tags: [wallets, security, bolt, identityserver, ledger, ef-core, caching, testing]
---

# Wallets Backend Audit Revalidation

## Audit Scope

- Baseline: `origin/develop` at `08a13824b452f8f9645af69abd7bb4e6877378a9`.
- Authority: `CLAUDE.md`, `rules/BackendGuidelines.md`, and the canonical VSA, EF Core, caching, trusted-invocation, remote `IDataContext`, and wrapper-test documents they require.
- Method: a fresh post-IdentityServer/post-Bolt review using the `xframework-audit-module` workflow, followed by remediation and regression testing on `codex/wallets-backend-post-bolt-audit`.
- Areas: financial authorization, actor/service/tenant propagation, Bolt and REST handlers, background/admin work, service and transport token reuse, VSA, services, EF/schema ownership, migrations, caching, validation, packages, generated reads, remote `IDataContext`, wrappers, and tests.

## Current Result

**Overall grade: C-.** This remediation removes the previously confirmed Critical defects and most High defects. Two High findings remain and should be handled before Wallets is treated as a complete least-privilege financial boundary.

| Severity | Count |
|---|---:|
| Critical | 0 |
| High | 2 |
| Medium | 8 |
| Low | 3 |

## Critical Findings

No current Critical finding remains after this remediation pass.

## High Findings

### H1. Generated Bolt and remote read paths do not enforce Wallets-specific actor capabilities

Sensitive Wallets entities now restrict generated REST routes to Admin/SuperAdmin roles, for example `Wallet` and webhook audit rows (`src/Modules/XFramework.Wallets/Wallets.Domain.Shared/Contracts/Wallet.cs:7-15`; `WalletPaymentWebhookEvent.cs:7-14`). The generated REST authorization policy, however, still establishes only an actor-tenant context (`src/SourceGenerators/XFramework.SourceGenerators/EntityEndpointGenerator.cs:603-619`), and the generated entity/Bolt and remote `IDataContext` paths do not consume those REST role declarations. The manual wallet reads are owner/admin scoped, but generic tenant-scoped generated reads remain available through service wrappers and remote queries.

**Impact:** an authenticated non-admin tenant actor that can invoke the generated query boundary may still read tenant-wide balances, operations, ledger entries, outbox payloads, webhook payloads, and provider diagnostics despite the REST role restriction.

**Missing coverage:** add a generated-handler and remote-`IDataContext` denial matrix for an ordinary actor, plus a capability-aware generated-endpoint contract shared by REST and Bolt.

### H2. Cumulative refund validation is not serialized with refund settlement

Case resolution now requires the policy-management capability, an independent decider, a completed original operation, a linked transaction when supplied, and a refund amount within the remaining debited amount (`src/Modules/XFramework.Wallets/Wallets.Api/Services/WalletWorkflowService.cs:1198-1236,1304-1374`). The `alreadyRefunded` sum is still read before `WalletLedgerService.ExecuteAsync` starts its serializable transaction and locks the wallet (`WalletWorkflowService.cs:1360-1372`; `WalletLedgerService.cs:44-119`).

**Impact:** two independently approved refund cases can both validate against the same remaining amount before either settlement is visible. Depending on transaction timing, both may settle and over-refund the original debit.

**Missing coverage:** add a concurrent partial-refund test and move original-operation/refundable-balance validation behind a lock inside the same transaction that writes the refund operation and postings.

## Medium Findings

### M1. Wallets applies migrations during application startup

`src/Modules/XFramework.Wallets/Wallets.Api/Program.cs:94` calls `EnsureDatabase<AppDbContext>()`, which can run migrations from each service replica.

**Impact:** replicas can race schema changes and bypass the migration-runner deployment authority.

**Missing coverage:** production composition should prove Wallets cannot migrate at runtime.

### M2. Reporting and reconciliation retain unbounded and N+1 query paths

Statements materialize all matching ledger entries (`WalletWorkflowService.cs:1377-1412`), settlement reporting loads deposit and withdrawal sets before combining/paging (`:1728-1821`), and reconciliation queries snapshot/ledger state per wallet (`WalletReconciliationService.cs:42-79`).

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

`Features/AdvancedWallets/Endpoint.cs` contains 274 lines of unrelated workflows, while `WalletWorkflowService.cs` is 2,779 lines. Boundary validators now cover the principal advanced mutation requests (`Features/AdvancedWallets/Validators.cs`), but ownership remains broad.

**Impact:** authorization, validation, and transaction changes have a large review and regression surface.

**Missing coverage:** no architecture guard limits multi-capability endpoint/service concentration.

### M6. Generated cache declarations remain decorative

Wallet entities declare `CacheDurationSeconds` and `CacheKeyPrefix`, for example `Wallet.cs:13-14`, but generated reads do not apply canonical cache keys, TTLs, or mutation invalidation.

**Impact:** the contracts imply caching that does not occur, while reference and financial reads continue to hit PostgreSQL.

**Missing coverage:** no cache hit, tenant-key, fail-open, TTL, or invalidation tests exist.

### M7. OpenAPI remains partial and coupled to persistence contracts

Wallets filters the OpenAPI document to selected DTO-backed routes (`src/Modules/XFramework.Wallets/Wallets.Api/Program.cs:29-43`) because generated EF entity schemas contain navigation cycles.

**Impact:** consumers cannot rely on a complete Wallets API contract.

**Missing coverage:** no complete document/schema test covers all public routes without EF navigation types.

### M8. Integration tests do not run production IdentityServer validation

The fixture exercises Bolt transport, trusted actor/service envelopes, wrapper calls, PostgreSQL, and migrations, but replaces IdentityServer validation with test providers (`src/Tests/Wallets.IntegrationTests/Infrastructure/WalletsTestFixture.cs:37-120,250-288`). Static contract filters also start Testcontainers because the assembly-wide fixture is unconditional (`:37,97-104`).

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

1. **Wallet event leakage is fixed.** The publisher requires tenant scope, and event retrieval resolves trusted tenant/actor context and limits ordinary actors to their own credential. Two-tenant regression coverage is included.
2. **Owner-created arbitrary refunds are blocked.** Resolution requires policy management, an independent decider, a completed original operation, optional transaction linkage, and remaining-refundable-amount validation. H2 records the remaining concurrency edge.
3. **Concurrent withdrawal approval no longer duplicates provider payout.** Approval atomically claims `Approved -> Settling`; only the winner invokes the provider, while hold replay reloads current workflow state.
4. **The public webhook route is HMAC-only again.** The HTTP endpoint is explicitly anonymous and tenantless; the internal Bolt path is separate and requires Portal, `wallets.admin`, `tenant.target`, and service-target tenant context.
5. **Invalid signatures cannot select an audit tenant.** Rejected events are written only when the provider is configured to a tenant, and raw payloads are capped at 256 KiB.
6. **Wallet administrative handlers now require canonical IdentityServer capabilities.** Values follow `{module}[.{subfeature}]:{view|create|update|delete|manage}`. Policy, outbox, reconciliation, approvals, workflow administration, reports, and core money handlers declare explicit capabilities; sensitive services also enforce them internally.
7. **Deposit and withdrawal creation is durably idempotent.** Tenant-scoped unique keys and request hashes replay identical requests and reject changed payloads. Migration `20260805170822_WalletWorkflowCreateIdempotency` contains only the four intended columns and two indexes.
8. **Batch requests are externally idempotent.** A top-level key is required; all-or-nothing mode replays the batch operation and partial mode derives stable per-item keys.
9. **Rejected financial attempts are persisted.** Ledger policy/approval rejection creates a rejected operation with request hash and decision metadata, without postings or outbox publication.
10. **Manual wallet reads are owner/admin scoped.** Wallet-by-ID and credential queries use trusted actor context instead of client authority.
11. **Advanced mutation validators now cover workflows, webhooks, cases, and batch requests.** Validation remains at the generated endpoint boundary and is backed by integration tests.
12. **Service and Bolt transport token acquisition remains cached and burst-controlled.** Independent singleton providers use per-key caches, refresh skew, single-flight acquisition, timeout, and failure backoff.
13. **Generated financial mutations remain sealed.** Wallet financial entities expose read actions only and are not allowlisted for remote mutation.
14. **Ledger and outbox safety remains intact.** Financial execution uses serializable transactions, deterministic wallet locks, balanced postings, snapshots, tenant-scoped request-hash replay, and atomic outbox creation.

## Verification Evidence

- `dotnet test src/Tests/Wallets.IntegrationTests/Wallets.IntegrationTests.csproj -m:1 /nr:false`: passed 103/103 against PostgreSQL through the `xeon-dev` Docker context.
- Focused webhook/outbox/reconciliation wrapper test: passed 1/1.
- `git diff --check`: passed.
- The EF migration was generated with EF Core tooling and applied successfully by the integration fixture. It contains only deposit/withdrawal idempotency columns and tenant-scoped unique indexes.
- `dotnet ef migrations has-pending-model-changes` through `XFramework.MigrationRunner`: passed with no pending model changes.
- No service was deployed or restarted.

## Audit Maintenance

Update this document whenever a confirmed finding is fixed, a Wallets security/ledger/workflow contract changes, or shared IdentityServer/Bolt behavior changes a conclusion. Mark a finding resolved only when implementation and regression/integration coverage are both present.
