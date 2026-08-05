---
title: "Inventario Backend Audit After Trusted Invocation Remediation"
date: 2026-08-05
category: workflow-issues
module: Inventario
problem_type: security_and_backend_compliance
component: inventario
severity: medium
status: remediated_with_deferred_item
applies_when:
  - "Changing Inventario services, wrappers, EF models, caching, or authorization"
  - "Validating Inventario after IdentityServer or Bolt security changes"
tags: [inventario, identityserver, security, bolt, ef-core, caching, testing]
---

# Inventario Backend Audit After Trusted Invocation Remediation

## Audit Scope

- Baseline: `origin/develop` at `c0e0a47e79c141fe6b40b24a79f353bd200b2629` (PR #398).
- Authority: `CLAUDE.md`, `rules/BackendGuidelines.md`, and the canonical VSA, EF Core, caching, remote data-context, wrapper, and Bolt documents they reference.
- Method: a fresh module audit using the `xframework-audit-module` workflow after the IdentityServer trusted-invocation, service-token, and Bolt-transport-token changes.
- Implementation: all accepted Critical, High, Medium, and Low remediation items were implemented and verified. Finding 5 remains documented and intentionally deferred by user decision.

## Current Result

**Grade: B+ - no Critical or High findings remain. One Medium least-privilege item is deferred.**

Inventario now consumes the centralized fail-closed trusted invocation context for HTTP and Bolt calls, uses dimensioned stock writes as inventory authority, protects receiving and reorder-rule concurrency at the database boundary, prevents Product remote-mutation bypass, degrades safely when product caching fails, and keeps broad report/planning predicates in PostgreSQL. The complete Inventario integration suite is green.

## Deferred Finding

### 5. Bolt handlers rely on broad defaults instead of operation-specific service policy

**Disposition:** noted and deferred by user decision on 2026-08-05. No remediation for this finding is included in the current Inventario change.

Inventario handlers use the secure generated defaults, which require a service identity, actor, actor tenant, feature gate, and capability authorization. They do not yet partition POS catalog, reservation, purchasing, stock, and administration calls with operation-specific `RequiredServiceScopes`, `AllowedServiceCallers`, or `RequiredActorCapabilities`.

**Residual impact:** service identity is authenticated, but caller authorization is not yet least-privilege partitioned between Inventario operation families. Revisit this if Inventario introduces distinct service trust zones or explicit POS caller allowlists.

## Remediated Findings

### Product stock is no longer a competing lossy authority

- `Product.StockQuantity` and `ProductResponse.StockQuantity` now use decimal precision.
- EF maps the snapshot as `numeric(18,4)`.
- Product creation rejects nonzero initial stock and instructs callers to use a warehouse/location-aware stock movement.
- Stock posting updates the compatibility product snapshot without integer rounding.
- The migration converts existing integer values without losing existing whole quantities.

Coverage proves product creation cannot create dimensionless opening stock and fractional postings preserve the product snapshot.

### Receiving now participates in optimistic concurrency

- Purchase-order and purchase-order-line mutations are attached before changes and rotate `ConcurrencyStamp` in receiving and status transitions.
- Stock, receipt, PO-line quantity, and PO status remain part of the same save boundary.

Coverage proves the service rotates both stamps and PostgreSQL rejects a stale concurrent PO-line update.

### Product writes are service-wrapper-only

- `Product` no longer has `[AllowRemoteDataContextMutation]`.
- Product create/update remain available through Inventario service wrappers.
- Remote Product create/update/delete integration contracts now assert rejection and no database mutation.

### Product cache failures are non-authoritative

- Cache get, set, remove, and prefix invalidation are best effort.
- Cache failures emit warnings but do not turn an already committed database write into a false `500` result.
- Cache keys use the `inventario:tenant:{tenantId}:...` namespace.

Coverage proves create and delete return their committed business result when cache operations throw.

### Product deletion protects operational dependencies

Deletion now returns conflict while the product has nonzero/reserved balances, active reservation allocations, lots, variants, active reorder rules, or open purchase-order lines.

### Invalid same-dimension transfers are rejected

The request validator and stock service both reject a transfer whose destination warehouse/location equals its source. Coverage proves no movement or save occurs.

### Reporting and planning filter in PostgreSQL

- Stock, movement, allocation, and expiry report predicates are applied to `IQueryable` before materialization.
- Report result sets and planning candidate sets have explicit caps.
- Lookup queries load only identifiers referenced by candidate rows.
- Planning filters rules and balances before materialization.

### Reorder-rule uniqueness is database-enforced

The active reorder-rule dimension index is unique, treats nullable dimensions as equal, and excludes soft-deleted rows. The migration deterministically soft-deletes pre-existing duplicates before creating the constraint. PostgreSQL integration coverage proves duplicate active dimensions fail.

### Routine query-filter bypass was removed

Inventario production API services contain no `IgnoreQueryFilters()` calls. Tenant and soft-delete behavior now uses the centralized fail-closed EF filters plus explicit business predicates where useful.

### Validation and metadata cleanup is complete

- `ReleaseReservationValidator` now validates the reservation id and optional reason length.
- Inventario no longer directly references `Microsoft.Extensions.Logging.Abstractions`.
- Swagger metadata identifies Inventario rather than IdentityServer.

### Integration authorization seed is current

The integration fixture seeds the configured trusted actor's `IdentityInformation`, `IdentityCredential`, and `IdentityRole`, allowing the centralized capability resolver to authorize normal wrapper commands. Separate denial tests remain responsible for unauthorized scenarios.

### POS catalog query projection is database-safe

Catalog search applies name, SKU, brand, variant-name, and variation-type predicates before projection. Private EF projection rows support SQL ordering and pagination; transport DTOs are created after materialization. This fixed the previously untranslated positional-record filter/order expressions.

## IdentityServer And Shared Security Findings Resolved Upstream

1. Request metadata no longer selects the effective tenant. `TrustedInvocationResolver` validates service identity, actor identity, tenant targeting, and capabilities before establishing `EffectiveTenantId`.
2. HTTP and Bolt authorizers both establish trusted invocation context and run generated feature gates.
3. EF tenant filters and writes fail closed through the trusted effective-tenant accessor.
4. Service access tokens and Bolt transport tokens use independent expiry-aware, single-flight caches, so Inventario does not create avoidable IdentityServer quota bursts.

## Verification Evidence

Runtime integration tests used a loopback-only SSH tunnel to Docker context `xeon-dev`; no deployed service was rebuilt or restarted.

- Inventario API build: passed, 0 warnings, 0 errors.
- Inventario integration project build: passed, 0 errors; generated change-tracker nullability warnings remain in generated code.
- Inventario API unit tests: **37/37 passed**.
- Inventario full integration suite: **62/62 passed**.
- EF pending-model check: no changes since migration `InventarioBackendRemediation20260805`.
- Production Inventario API `IgnoreQueryFilters()` count: **0**.
- Product remote-mutation attributes: **0**.

## Remaining Follow-Up

1. Revisit deferred finding 5 only when operation-specific service policy is approved.
2. Address generated integration change-tracker nullability warnings in the source generator rather than suppressing them in Inventario.
3. Keep this audit updated when Inventario backend behavior, authorization, wrappers, or persistence rules change.
