---
title: "POS Backend Post-IdentityServer Revalidation"
date: 2026-08-05
category: workflow-issues
module: POS
problem_type: security_and_backend_compliance
component: pos
severity: medium
status: partially_resolved
tags: [pos, bolt, identityserver, ef-core, inventario, wallets, testing]
---

# POS Backend Post-IdentityServer Revalidation

## Audit Scope

- Baseline: `origin/develop` at `c0e0a47e79c141fe6b40b24a79f353bd200b2629` (PR #398).
- Implementation worktree: `codex/pos-audit-post-identityserver`; the shared dirty checkout was not modified.
- Authority: `CLAUDE.md`, `rules/BackendGuidelines.md`, and the canonical VSA, EF Core, Bolt, caching, and wrapper-boundary documents they require.
- Method: fresh `xframework-audit-module` review followed by authorized remediation and revalidation.
- Overall score after remediation: **B**. No verified Critical or High finding remains. Three Medium risks and one Low maintainability issue remain.

## Summary

The current IdentityServer/Bolt foundation remains valid: trusted invocation context is the tenant authority, service and actor identities are separate, generated Bolt handlers authorize before service resolution, and service/transport tokens use independent singleton caches with collapsed concurrent acquisition.

The remediation closes the previous duplicate-capture, over-refund, total-reconciliation, capability, terminal-reservation, register-boundary, and cart-idempotency findings. POS now has deterministic payment posting, server-derived refund allocations, captured-payment caps, operation capabilities on HTTP and Bolt, replay-safe Inventario terminal operations, generated remote register reads, transaction line limits, and real PostgreSQL/Bolt integration fixtures.

POS still needs Docker-executed cross-module checkout/return workflow coverage before the production-readiness claim is complete. Current integration fixtures prove migration/constraint behavior and generated Bolt authorization, but not the full Inventario reservation plus Wallets posting workflow.

## Critical Findings

No current Critical findings.

## High Findings

No current High findings.

## Medium Findings

### M1. Runtime integration coverage does not yet execute checkout and return across all three modules

- Evidence: the new PostgreSQL fixture applies migrations and verifies the active payment constraint and cart request hash at `src/Tests/POS.IntegrationTests/PosPersistenceIntegrationTests.cs:55`.
- Evidence: the new hosted Bolt fixture calls `GetPosRegister` through the generated wrapper and verifies actor-capability denial at `src/Tests/POS.IntegrationTests/PosBoltRuntimeIntegrationTests.cs:108`.
- Evidence: both fixtures are intentionally skipped when no Testcontainers-compatible Docker endpoint exists at `src/Tests/POS.IntegrationTests/PosPersistenceIntegrationTests.cs:33` and `src/Tests/POS.IntegrationTests/PosBoltRuntimeIntegrationTests.cs:64`.
- Impact: local green tests do not yet prove reservation, wallet capture/refund, compensation, and recovery behavior through running POS, Inventario, Wallets, Bolt, and PostgreSQL services.
- Missing coverage: concurrent checkout replay with one observed Wallets posting; payment-success/fulfillment-failure recovery; concurrent partial returns with one bounded refund; tenant/feature denial; and token-acquisition counts under POS load.

### M2. A concurrent different-key partial return can surface a database retry as an internal error

- Evidence: return allocation runs in a serializable transaction, preserving the financial invariant, but the save catch only converts a conflict when it can reload the same idempotency key at `src/Modules/XFramework.POS/POS.Api/Services/PosReturnsService.cs:124-144`.
- Evidence: a serialization failure from a competing return with a different key has no same-key replay row and is rethrown at `src/Modules/XFramework.POS/POS.Api/Services/PosReturnsService.cs:138-139`.
- Impact: PostgreSQL prevents over-return, but one legitimate concurrent cashier request can receive a 500 instead of a deterministic retryable conflict.
- Missing coverage: PostgreSQL test with two different idempotency keys returning the same remaining quantity, asserting one success and one explicit 409/retry response.

### M3. Inventario enrichment and product validation remain sequential within the new request limits

- Evidence: catalog stock enrichment still performs sequential `GetStockBalances` calls at `src/Modules/XFramework.POS/POS.Api/Services/PosCatalogService.cs:85`.
- Evidence: cart and sale line construction still perform sequential product lookups at `src/Modules/XFramework.POS/POS.Api/Services/PosCartService.cs:419` and `src/Modules/XFramework.POS/POS.Api/Services/PosSalesService.cs:363`.
- Evidence: cart, sale, and return requests are now capped at 100 lines at `src/Modules/XFramework.POS/POS.Api/Features/PosValidators.cs:74-75`, `:170-171`, and `:231-232`.
- Impact: work is bounded, but high-line transactions and 100-item catalog pages can still have avoidable cumulative Bolt latency.
- Missing coverage: latency/load tests at the supported maximum and a batched or bounded-concurrency Inventario read contract.

## Low Findings

### L1. POS endpoint and validator files remain flattened rather than feature-centric VSA

- Evidence: all POS endpoint handlers remain in `src/Modules/XFramework.POS/POS.Api/Features/PosEndpoints.cs:10-309`, and validators remain in `src/Modules/XFramework.POS/POS.Api/Features/PosValidators.cs:11-267`.
- Impact: feature ownership and navigation become harder as the module grows; runtime behavior is unaffected.
- Required direction: split touched slices into `Features/<Area>/<Action>` incrementally, without a broad structure-only rewrite.

## Resolved Findings

### Resolved: C1 concurrent duplicate payment capture

- Payment references are now deterministic from the sale at `src/Modules/XFramework.POS/POS.Api/Services/PosServiceHelpers.cs:34-35`.
- V1 one-tender semantics are database-enforced by `UX_POS_Payment_Tenant_Sale_Active` at `src/Modules/XFramework.POS/POS.Domain.Shared/Configurations/PosEntityConfigurations.cs:190-194`.
- Sale/payment uniqueness conflicts reload and resume the persisted sale at `src/Modules/XFramework.POS/POS.Api/Services/PosSalesService.cs:103-119` and `:160-174`.
- The generated EF migration creates the constraint at `src/Kernel/XFramework.Domain/Migrations/20260805152712_HardenPosPaymentAndCartIdempotency.cs:26-32`.

### Resolved: C2 over-return and over-refund

- Duplicate return lines and caller-authored tax are rejected at `src/Modules/XFramework.POS/POS.Api/Features/PosValidators.cs:233-241` and rechecked by the service.
- Refund subtotal/tax are allocated from immutable sale values at `src/Modules/XFramework.POS/POS.Api/Services/PosServiceHelpers.cs:123-177`.
- Cumulative refunds are capped to captured payments at `src/Modules/XFramework.POS/POS.Api/Services/PosReturnsService.cs:108-121`.
- Existing return allocations are grouped once and final partial returns receive the exact rounding remainder at `src/Modules/XFramework.POS/POS.Api/Services/PosReturnsService.cs:289-342`.

### Resolved: H1 monetary totals did not reconcile

- Sale totals now sum line totals, including line discounts/tax, then proportionally account for header adjustments at `src/Modules/XFramework.POS/POS.Api/Services/PosSalesService.cs:91-101` and `src/Modules/XFramework.POS/POS.Api/Services/PosServiceHelpers.cs:123-151`.
- Cart totals now use `Sum(LineTotal) - header discount + header tax` at `src/Modules/XFramework.POS/POS.Api/Services/PosCartService.cs:654-660`.
- Pure financial invariant tests cover header/line reconciliation and partial-return rounding.

### Resolved: H2 missing operation-level actor capabilities

- Stable POS capability names are defined in `src/Modules/XFramework.POS/POS.Domain.Shared/Contracts/PosAuthorizationCapabilities.cs:3-20`.
- Every endpoint now declares full actor capabilities for generated Bolt authorization and standard `Capability` metadata for HTTP feature authorization; representative declarations are at `src/Modules/XFramework.POS/POS.Api/Features/PosEndpoints.cs:13-18`, `:168-173`, and `:254-259`.

### Resolved: H3 terminal Inventario reservation replay

- Releasing an already released reservation and fulfilling an already fulfilled reservation return success without new stock posting at `src/Modules/XFramework.Inventario/Inventario.Api/Services/ReservationService.cs:127-139` and `:161-173`.
- Regression tests verify no duplicate movements and no second save.

### Resolved: M1 foreign-schema register reads and wallet ownership

- Register validation uses generated IdentityServer, Wallets, and Inventario read wrappers at `src/Modules/XFramework.POS/POS.Api/Services/PosRegisterService.cs:184-225`.
- Cash drawer ownership must match the merchant credential at `src/Modules/XFramework.POS/POS.Api/Services/PosRegisterService.cs:207-208`.

### Resolved: M3 cart idempotency

- `PosCart.RequestHash` is persisted and mapped; same-key/different-payload requests conflict.
- Concurrent same-key inserts reload and replay the winner at `src/Modules/XFramework.POS/POS.Api/Services/PosCartService.cs:33-44` and `:97-117`.

## IdentityServer And Trusted Invocation Revalidation

The prior post-IdentityServer conclusions remain resolved and applicable:

- Raw request metadata is not a tenant authority; POS uses the trusted effective tenant and rejects metadata mismatch.
- Generated Bolt handlers establish authorization and trusted tenant context before resolving POS services.
- Normal POS calls require separate destination-service and actor identities.
- Sender binding, IdentityServer-backed actor/session validation, exact feature gates, and remote `IDataContext` scope checks remain centralized.
- Service access tokens and Bolt transport tokens remain independently cached singleton providers with per-key in-flight acquisition collapse; POS introduces no per-request token provider.

## Verification

- `dotnet test src/Tests/POS.Api.Tests/POS.Api.Tests.csproj -m:1 /nr:false` - passed 24/24.
- `dotnet test src/Tests/Inventario.Api.Tests/Inventario.Api.Tests.csproj -m:1 /nr:false` - passed 39/39.
- `dotnet test src/Tests/POS.IntegrationTests/POS.IntegrationTests.csproj -m:1 /nr:false` - passed 12, skipped 2 Docker-required runtime tests.
- `dotnet test src/Tests/XFramework.Core.Tests/XFramework.Core.Tests.csproj -m:1 /nr:false` - passed 338/338.
- `dotnet ef migrations has-pending-model-changes ... --no-build` - no pending model changes after rebuilding the migration runner.
- `git diff --check` - passed.

## Deployment Preconditions

- Run the two Docker-backed POS integration tests in CI or another Testcontainers-capable environment before merge.
- Check deployed POS data for more than one active payment row per `(TenantId, SaleId)` before applying the new unique index; the EF-generated migration intentionally does not delete or rewrite financial records.
- Ensure cashier, supervisor, returns, and register-administrator roles receive the new POS capabilities before rollout.
