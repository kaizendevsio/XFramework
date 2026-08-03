---
title: "IdentityServer Backend Audit And Remediation"
date: 2026-07-31
category: workflow-issues
module: IdentityServer
problem_type: security_and_backend_compliance
component: identityserver
severity: critical
status: completed
tags: [identityserver, security, bolt, ef-core, caching, testing]
---

# IdentityServer Backend Audit And Remediation

## Audit Scope

- Baseline: `origin/develop` at `102bcd1040babccfa54d894c01036c851557be3e`.
- Authority: `CLAUDE.md`, `rules/BackendGuidelines.md`, and the canonical documents they reference.
- Method: a fresh post-Bolt audit using the `xframework-audit-module` workflow, followed by implementation, runtime verification, and three independent reviewer passes.
- Areas: VSA structure, endpoints, services, EF Core and schema ownership, caching, validation, packages, tests, remote `IDataContext`, generated wrappers, Bolt sender binding, and service authentication.

## Final Result

All confirmed Critical, High, Medium, and Low compliance findings from this audit are resolved. No unresolved IdentityServer defect remains from the approved remediation scope.

Two observations remain as optional future design work, not current `BackendGuidelines.md` violations:

- `AuthService` is still large and may benefit from incremental slice extraction when a related workflow is next changed.
- OpenAPI exposure remains a product-level decision now that sensitive entity members and mutation boundaries are protected.

## Resolved Critical Findings

1. Token-mode authentication is tenant-scoped and cannot select a credential without password or trusted proof validation.
2. Password changes require authenticated/trusted actor context; callers cannot bypass verification with a client-controlled flag.
3. Tenant, credential, role, authorization, avatar, and signing-key administration enforce HTTP authorization, narrow Bolt scopes, capability checks, and server-derived tenant context.
4. Credential hashes, refresh tokens, verification tokens, signing private keys, and session payloads are excluded from public serialization and safe response DTOs.

Primary implementation areas:

- `IdentityServer.Api/Features/Auth`
- `IdentityServer.Api/Features/Authorization`
- `IdentityServer.Api/Features/Credentials`
- `IdentityServer.Api/Features/ServiceIdentity`
- `IdentityServer.Domain.Shared/Contracts`

## Resolved High Findings

1. Authentication and session validation now reject disabled/deleted tenants, credentials, and stale cached tenant state.
2. Avatar workflows use Storage wrappers and durable claim/cleanup outboxes; IdentityServer no longer directly mutates Storage-owned entities or maintains a cross-schema avatar foreign key.
3. Verification rows are tenant-owned, use correct timestamps/types, have bounded attempts, single-use concurrency, and durable delivery outboxes.
4. Service-token signing keys use provisioned private-key files, bounded publication, overlap-aware retirement, serialized rotation, and database concurrency controls.
5. Remote `IDataContext` handlers bind service-token identity to the Hub-authenticated sender and reject caller mismatch.
6. Authentication, password-reset, and service-token routes use distributed, fail-closed throttling with trusted server-observed keys.
7. Mutable generated identity surfaces are covered by module feature gates and capability authorization.
8. Communications idempotency no longer returns false success after a failed delivery, and Communications persistence redacts password-reset recipient/body/template data.

Primary implementation areas:

- `IdentityServer.Api/Services/AuthService.cs`
- `IdentityServer.Api/Services/ServiceIdentityService.cs`
- `IdentityServer.Api/Services/*OutboxDispatcher.cs`
- `XFramework.Core/DataContext`
- `XFramework.Core/RateLimiting`
- `XFramework.Communications/Communications.Api/Services/CommunicationsService.cs`
- `XFramework.Storage/Storage.Api`

## Resolved Medium Findings

1. Session-type and remote entity caches are tenant-aware and mutations invalidate entity prefixes consistently.
2. Effective-capability resolution batches database work instead of querying per matrix cell.
3. Refresh/reset proofs are protected by expiry, hashing or non-replayable state, and optimistic/database concurrency.
4. Persistence results and cancellation are propagated through audited workflows.
5. Tenant-first composite indexes, active-row uniqueness filters, and corrected role/metadata dedup migrations cover hot query shapes.
6. Runtime services no longer apply migrations; `XFramework.MigrationRunner` remains authoritative.
7. Intentional schema ownership exceptions are documented in `identityserver-schema-ownership-exceptions.md`.
8. Remote query `Skip` is capped at 10,000 and result/query bounds are enforced server-side.
9. Cleanup outbox dispatch includes disabled/deleted rows so lifecycle cleanup cannot be stranded.
10. Public validators are null-safe for collections and elements and bound capability, session, signing-key, and module-feature inputs.
11. `ServiceIdentityService` derives admin authority from trusted `RequestMetadata` or the trusted service invocation resolver, not `IHttpContextAccessor`.
12. Production composition coverage uses the Production environment and retains required outbox hosted services.
13. Pull-request CI excludes only `Kind:ExtendedIntegration`; manual runs with no explicit filter execute the full suite.

## Resolved Low Findings

1. The unnecessary shared `System.Net.Http.Json` package reference was removed.
2. Generated development key directories are ignored and no generated PEM files remain under source-tree `.data` directories.
3. Wrapper completeness and standard remote-data-context smoke tests run in the normal integration tier.
4. Registry configuration group mutations have explicit cross-tenant create/update/remove regression coverage.

## Post-Bolt Findings Reverified As Resolved

- Generated Bolt handlers validate destination service tokens and bind callers to Hub-authenticated senders.
- Bolt transport uses centrally issued, file-backed RSA tokens with discovery/JWKS and no legacy shared-secret fallback.
- Hub disconnect and pending-route behavior no longer permits indefinite caller hangs.
- Identity authorization endpoints require `identity.admin` or a narrower approved scope and trusted actor context.
- Service authentication configuration is centralized and fails closed when required credentials or signing material are unavailable.

## Independent Reviewer Follow-Up

Three read-only reviewers rechecked persistence, security, and VSA/contract findings after implementation.

- Persistence reviewer: all five follow-up findings resolved, including active-only Storage metadata dedup, cleanup eligibility, remote `Skip` bounds, cache invalidation, and role dedup timestamps.
- Security reviewer: all four follow-up findings resolved, including Communications redaction/idempotency/error handling and generated key hygiene.
- VSA/contracts reviewer: production composition, trusted service metadata, CI tiers, tenant mismatch coverage, and input bounds resolved. A final null-element validator gap was then fixed and covered by `AuthorizationValidatorTests`.

## Remediation Checklist

- [x] Close token-mode and password-change account takeover paths.
- [x] Authorize HTTP and Bolt administration with server-derived tenant context.
- [x] Replace secret-bearing public entity responses with safe contracts.
- [x] Enforce active tenant and credential lifecycle and cache invalidation.
- [x] Move avatar persistence behind Storage-owned wrappers and durable outboxes.
- [x] Fix verification persistence, delivery, replay, and concurrency behavior.
- [x] Protect and serialize signing-key lifecycle operations.
- [x] Bind remote `IDataContext` calls to authenticated Bolt sender provenance.
- [x] Add distributed authentication and recovery throttling.
- [x] Complete feature-gate and explicit remote-mutation contracts.
- [x] Make caches tenant-aware and batch capability resolution.
- [x] Enforce persistence results, cancellation, concurrency, and tenant-first indexes.
- [x] Keep migrations in `XFramework.MigrationRunner` and document schema exceptions.
- [x] Add wrapper, contract, unit, migration, runtime, and production-composition coverage.
- [x] Complete independent post-fix reviewer checks.

## Verification Evidence

Runtime tests used the documented loopback-only SSH/socat Testcontainers tunnel to Docker context `xeon-dev`; no deployed service was rebuilt or restarted.

- IdentityServer integration: 188 passed.
- IdentityServer unit: 87 passed.
- Storage integration: 14 passed.
- Communications: 105 passed.
- XFramework Core: 272 passed.
- Source generators: 42 passed.
- Portal contracts: 45 passed.
- Bolt: 547 passed, 7 skipped in the full transport suite run for the merged Bolt lifecycle changes.
- Portal build: succeeded with 0 warnings and 0 errors.
- EF Core: no pending model changes using `XFramework.MigrationRunner` as the startup project.
- IdentityServer dependency graph: no vulnerable or deprecated packages from the configured package sources.
- Repository hygiene: `git diff --check` passed; no temporary diagnostic markers or source-tree `.data` PEM files remain.

## Test Infrastructure Note

The expanded runtime suite exceeded the production Bolt transport token's 120-second lifetime and consistently placed one wrapper RPC inside the expected Hub disconnect/reconnect window. The shared IdentityServer integration fixture now uses signer-backed 30-minute test-only transport tokens so suite length does not create an implicit timing test. Central transport token issuance and discovery remain explicitly verified elsewhere in the fixture and dedicated Bolt/security tests. Production token lifetime and Hub behavior were not changed.
