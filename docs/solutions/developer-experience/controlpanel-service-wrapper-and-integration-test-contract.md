---
title: "ControlPanel Service Wrapper And Integration Test Contract"
date: 2026-06-20
category: developer-experience
module: XFramework
problem_type: convention
component: controlpanel_testing
severity: high
applies_when:
  - "Building ControlPanel pages that call module APIs or mutate module data"
  - "Adding module integration tests for wrappers, remote IDataContext, or ControlPanel smoke flows"
  - "Deciding whether a Blazor page should use a service wrapper or IDataContext"
tags: [control-panel, service-wrapper, datacontext, integration-tests, nunit, wrappers]
status: current
---

# ControlPanel Service Wrapper And Integration Test Contract

## Decision Rule

ControlPanel pages must use the most business-aware API available.

- Use module service wrapper methods for business operations and custom endpoints.
- Use `IDataContext.Query<T>()` for read/query UI when the entity is registered for remote data-context querying.
- Use `IDataContext.Add/Update/Remove` only for simple generated CRUD on entities explicitly allowlisted for remote mutation.
- Do not use `IDataContext.SaveChangesAsync()` to bypass endpoint validators, feature gates, idempotency, allocation logic, ledger posting, tenant derivation, or status workflows.

The Blazor `IDataContext` migration removed generated per-entity CRUD wrapper properties. It did not make `IDataContext` the default write path for every ControlPanel action.

## Wrapper First For Business Workflows

A ControlPanel action should call a service wrapper when any of these are true:

- A request contract exists, such as `CreateInventoryReorderRuleRequest`, `PostStockMovementRequest`, or `ReceiveInventoryRequest`.
- The action has module feature gates, tenant-module gates, auth metadata, or role checks.
- The action creates ledger rows, allocation rows, notification rows, audit rows, or derived snapshots.
- The action has idempotency or duplicate-prevention semantics.
- The action has a state machine or status transition.
- The action validates more than scalar shape or required fields.

Examples:

- Inventario reorder rules: use `IInventarioServiceWrapper.CreateInventoryReorderRule`, not `DataContext.Add(new InventoryReorderRule(...))`.
- Inventario stock and reservations: use wrapper methods so movement ledgers, balances, allocations, FEFO, and idempotency run.
- Purchasing and receiving: use wrapper methods so purchase-order status and receipt movements stay consistent.
- Wallet operations: use wrapper methods for deposits, withdrawals, holds, transfers, closes, and status changes.
- Identity authentication, credentials, sessions, and role assignment workflows should use the relevant IdentityServer wrapper operation when one exists.

## Product-Centric Inventory UI

Inventario product detail is the operational hub for product-specific inventory workflows. When adding UI for product stock, lots/batches, receiving, replenishment, variations, or product transaction history:

- Keep the operator on `/inventario/products/{id}` or one of its detail-sidebar section routes.
- Use product-preselected dialogs for product-specific stock movements, receiving, lot creation, and reorder rules.
- Use shared entity pickers for dependency entities such as warehouse, location, lot, supplier, and purchase order.
- Dependency creation belongs to picker-owned `Create New` dialogs and picker-owned `Advanced Search` dialogs. Do not embed warehouse/location/supplier create forms directly inside product stock, receiving, or replenishment forms.
- Picker `Advanced Search` dialogs must expose domain-specific columns, filters, and sorting. A plain single text search in a modal is not enough; the dialog should state what it searches and provide useful finder columns such as code, name, status, warehouse/location scope, expiry, supplier, active/default flags, and created dates where applicable.
- Do not put existing-record edit workflows into list-page modals. List pages should navigate to detail pages; detail pages are the edit/operation surface.
- Keep wrapper calls authoritative for stock, receiving, lots, reservations, planning, purchasing, reporting, and other advanced workflows.
- Keep direct `IDataContext` mutations limited to the explicit Inventario catalog allowlist unless the contract is deliberately expanded and covered by tests.

## IDataContext Is Still Valid

`IDataContext` remains the preferred ControlPanel API for:

- Remote read/query composition.
- Simple catalog-style CRUD where direct mutation is part of the intended contract.
- Entities marked with `[AllowRemoteDataContextMutation]` and no richer endpoint exists for that action.

Before using `DataContext.Add/Update/Remove`, verify all of the following:

1. The entity is intentionally registered for remote mutation.
2. No wrapper method/request contract exists for the same user action.
3. The write does not bypass module feature gates, tenant checks, validators, idempotency, or derived data updates.
4. Integration tests cover the exact remote `IDataContext` mutation path.

If any item fails, use or add a wrapper endpoint instead.

## Integration Test Levels

Keep fast CI useful, but make broad runtime validation available on demand.

### Standard CI Suites

Run these on PRs and normal CI:

- Unit tests for services, validators, and core infrastructure.
- Focused module integration smoke tests for critical wrappers.
- A minimal remote `IDataContext` smoke query and mutation for each module that exposes remote data-context access.

These tests should stay small enough to run frequently.

### Extended Manual Runtime Suites

Create an extended integration suite for broad module validation. It may be slow and should be manually dispatched or explicitly filtered, not part of every PR by default.

The extended suite should cover:

- Every public wrapper request contract, at least one success path and one meaningful failure path.
- A generated wrapper coverage matrix by enumerating every module `IBoltRequest` contract and matching each one to a direct service-wrapper integration test.
- Every ControlPanel write path, including whether it uses the wrapper or remote `IDataContext`.
- Every entity intentionally allowlisted for remote `IDataContext` mutation: create, update, remove or soft-delete where supported.
- Remote `IDataContext` rejection for entities that must not be remotely mutated.
- Tenant isolation, feature gates, auth metadata, and role-sensitive behavior.
- Idempotent replay and conflict cases for workflows that accept idempotency keys.

Use NUnit categories so the suite can be targeted:

```text
Kind:Integration
Kind:ExtendedIntegration
Module:IdentityServer
Module:Wallets
Module:Inventario
Area:Wrappers
Area:DataContext
Area:ControlPanelContract
Area:FeatureGates
```

Example commands:

```bash
dotnet test src/Tests/Inventario.IntegrationTests/Inventario.IntegrationTests.csproj --filter "TestCategory=Kind:ExtendedIntegration"
dotnet test src/Tests/Inventario.IntegrationTests/Inventario.IntegrationTests.csproj --filter "TestCategory=Area:DataContext"
dotnet test src/Tests/Wallets.IntegrationTests/Wallets.IntegrationTests.csproj --filter "TestCategory=Area:Wrappers"
```

## Required Agent Audit Before ControlPanel Changes

When touching a ControlPanel module page:

1. Search the page for `DataContext.Add`, `DataContext.Update`, `DataContext.Remove`, and `SaveChangesAsync`.
2. Search the module shared contracts for matching `*Request` types.
3. Prefer the matching service wrapper method when a request/endpoint exists.
4. Check whether any direct `IDataContext` mutation entity has `[AllowRemoteDataContextMutation]`.
5. Add or update tests for the exact path the page uses.
6. If claiming wrapper coverage is complete, enumerate all `IBoltRequest` contracts and prove each generated wrapper method appears in the module integration tests.
7. Browser-smoke the page after the change when the page is user-facing.

Useful searches:

```bash
rg -n "DataContext\.(Add|Update|Remove)|SaveChangesAsync\(" src/Presentation/ControlPanel.Server/Components/Pages
rg -n "class .*Request|record .*Request" src/Modules/<Module>/<Module>.Domain.Shared/Contracts
rg -n "\[AllowRemoteDataContextMutation\]" src/Modules/<Module>
```

## Failure Pattern To Avoid

Do not consider wrapper tests sufficient when the UI uses `IDataContext`.

If a test calls:

```csharp
await wrapper.CreateInventoryReorderRule(request);
```

but the UI calls:

```csharp
DataContext.Add(new InventoryReorderRule { ... });
await DataContext.SaveChangesAsync();
```

then the test does not cover the UI path. The UI can fail even when the wrapper test passes.
