# Inventario Module Agent Guide

This file applies only to `src/Modules/XFramework.Inventario/**`. Root `AGENTS.md`, `CLAUDE.md`, `docs/solutions/conventions/xframework-best-practices.md`, `rules/BackendGuidelines.md`, and `rules/UiGuidelines.md` still apply.

Update this file in the same PR whenever Inventario behavior, contracts, feature rules, UI expectations, deployment wiring, integration patterns, bug fixes, or production lessons change. Treat stale guidance here as a module bug.

## Read First

- Start with the repository root `AGENTS.md` and `CLAUDE.md`.
- Read `rules/BackendGuidelines.md` before changing services, EF entities, configurations, migrations, wrappers, Bolt handlers, caching, or runtime setup.
- Read `rules/UiGuidelines.md` before changing Portal or Blazor UI.
- Read `docs/solutions/developer-experience/portal-service-wrapper-and-integration-test-contract.md` before changing Portal write paths, service wrappers, or remote `IDataContext` usage.
- Current source code wins over this guide. If source and this guide disagree, fix this guide as part of the change.

## Module Purpose

Inventario owns tenant-scoped inventory operations: product catalog, categories, variants, variation types, warehouses, locations, lots/batches, stock balances, stock movements, reservations, allocations, purchasing, receiving, planning/reorder rules, and inventory reports.

Inventario is not the owner of identities, tenant membership, payments, wallet balances, messaging delivery, or binary storage. Store cross-module links as IDs and use the owning module wrapper or approved read model when integration is needed.

## Project Map

- `Inventario.Api`: VSA endpoints, validators, domain services, feature gates, health checks, generated endpoint registration, and Bolt/data-context runtime setup.
- `Inventario.Domain.Shared`: EF entities, configurations, enums, request contracts, response contracts, report rows, and shared DTOs.
- `Inventario.Integration`: generated `IInventarioServiceWrapper` integration surface for Portal and cross-module callers.
- `src/Tests/Inventario.IntegrationTests`: PostgreSQL-backed integration tests, wrapper completeness tests, remote `IDataContext` tests, and Portal contract tests.
- `src/Presentation/XFramework.Portal/Components/Pages/Inventario`: Portal Inventario UI. UI guidance lives in root `rules/UiGuidelines.md`; keep module-specific workflow details here.

## Ownership Boundaries

- Inventario owns only the `Inventario` database schema.
- IdentityServer remains the source of truth for tenants, credentials, roles, and user identity.
- Communications/Notifications own external messages and delivery. Inventario may call approved wrappers for notifications, but must not duplicate messaging infrastructure.
- Wallets owns money movement and wallet ledger behavior. Inventario sale price/cost data is catalog/inventory metadata, not wallet balance data.
- Store `TenantId`, `CredentialId`, and other cross-module IDs as links. Do not add cross-schema foreign keys unless an explicit architecture decision says so.
- All reads and writes must be tenant-scoped. Do not trust client-supplied tenant IDs on protected or business-critical paths when trusted metadata/context exists.

## Domain Rules

- `Product` is the catalog root.
- `ProductVariationType` is the reusable or product-local type/category for variants, such as size, color, or packaging.
- `ProductVariation` is a sellable optional product dimension. `ProductVariationId = null` means base product stock.
- Variant-aware inventory must keep product and variant consistency across lots, balances, movements, reservations, purchase order lines, receiving lines, reorder rules, product transactions, and reports.
- Use absolute variant prices. Preserve legacy `VariationType` and `AdditionalPrice` compatibility only where existing migration/API behavior requires it.
- `Warehouse` contains `InventoryLocation` records. A stock operation must validate that locations belong to the selected warehouse when both are supplied.
- `InventoryLot` is traceability data. Lot numbers may repeat only where the configured uniqueness allows product/variant/warehouse scope. Lot validation must ensure the lot belongs to the same product and variant as the requested stock operation.
- `StockBalance` is the current snapshot for product plus optional variant plus warehouse/location/lot dimensions.
- `InventoryMovement` and `ProductTransaction` are operational/audit records. Do not update balances without writing the corresponding movement/transaction path.
- Reservations and allocations must allocate only matching product, optional variant, warehouse/location, and lot stock. Do not let product-level reservations consume a different variant accidentally.
- Purchasing and receiving must preserve variant, lot, warehouse, and location identity through purchase order lines, receiving lines, stock movements, and resulting balances.
- Reports should support product aggregate views and variant-filtered views where the underlying data is variant-aware.

## API And Service Rules

- Use VSA feature folders under `Inventario.Api/Features`.
- Public request/response contracts live under `Inventario.Domain.Shared/Contracts`.
- Wrapper-callable requests must implement `IBoltRequest` and live in shared contracts.
- Generated endpoints should use local `[Map*]` attributes and `[BoltHandler]` where the operation is exposed through the wrapper.
- Endpoint handlers should validate, call the appropriate service, and map `Result` values. Do not put business rules directly in endpoint handlers.
- Use the existing domain services as the business authorities:
  - `ProductService` for product create/update behavior.
  - `ProductVariationService` for variation types and variants.
  - `StockPostingService` for stock movements, balances, and stock ledger behavior.
  - `InventoryLotService` for lot creation and lookup behavior.
  - `WarehouseService` for warehouses and locations.
  - `ReservationService` and `InventoryAllocationService` for reservations and allocations.
  - `PurchasingService` for purchase orders and receiving.
  - `InventoryPlanningService` for reorder rules and suggestions.
  - `InventoryReportingService` for report read models.
- Services should return `Result<T>` or `Result`; expected business failures should not throw.
- Validators belong beside the feature endpoint and must enforce tenant/product/variant/warehouse/location/lot consistency.
- Use projections and pagination for list/report queries. Do not add unbounded reads for operational tables.

## Bolt, Wrappers, And DataContext

- Portal and cross-module business operations must use `IInventarioServiceWrapper` when a request contract exists.
- Do not use direct remote `IDataContext.Add`, `Update`, `Remove`, or `SaveChangesAsync` to bypass Inventario validators, feature gates, tenant checks, stock posting, lot matching, reservation allocation, purchase/receiving status, idempotency, or report snapshot rules.
- Remote `IDataContext.Query<T>()` is acceptable for tenant-scoped read UI and report/detail projections when the query shape is tested.
- Direct remote mutation is acceptable only for intentionally allowlisted simple catalog/admin entities and only when no richer wrapper request exists.
- Keep wrapper completeness coverage current. When adding a new `IBoltRequest`, add a direct `IInventarioServiceWrapper` integration test and keep `WrapperCoverageCompletenessTests` green.
- Do not assume HTTP health means wrapper health. Bolt handler registration and generated wrapper routing must be covered by integration tests for wrapper changes.
- Pass `hostEnvironment` to `AddXFrameworkBoltClient` so non-Development startup validates secure `wss://` transport configuration. Do not bypass that validation with the environment-free overload or a plaintext client URL.
- POS and sales modules must consume Inventario through wrappers, not cross-module writes. Use `SearchSellableProducts`, `GetSellableProduct`, and `GetProductVariations` for POS catalog line selection, then use the reservation, stock movement, balance, and movement query wrappers with stable `ReferenceType`/`ReferenceId` values for inventory effects and follow-up reads. Do not add POS-owned sale, payment, tax, or receipt behavior to Inventario.

## Feature Gates And Tenant Modules

- Inventario APIs are gated by `TenantModuleFeatureKeys.Inventario`.
- Subfeature routes include warehousing, stock balances, movements, reservations, traceability, planning, reporting, purchasing, and receiving.
- Portal pages must hide or disable Inventario workflows when the tenant feature or subfeature is unavailable.
- If feature keys, route prefixes, or tenant-module behavior change, update `InventarioFeatureGateRoutes`, Portal navigation, integration tests, and this guide.

## Portal Rules

- Follow `rules/UiGuidelines.md` for every Inventario UI change.
- Use BlazorBlueprint `BbDataGrid` for lists, reports, finder dialogs, and data-heavy detail tabs. Enable native filtering on useful business columns.
- Use shared `XfEntityPicker<TItem>` for dependency entity selection: product, variant, warehouse, location, lot, supplier, purchase order, reservation, and similar domain entities.
- Do not make users type raw GUIDs for domain entities.
- Keep product-specific workflows on `/inventario/products/{id}` or one of its detail-sidebar routes when practical.
- Product detail has one canonical section-navigation surface: the shell/sidebar `ProductDetailSidebar`. Do not add duplicate in-page section navigation or redundant Back to Products buttons.
- Dependency creation belongs in picker-owned Create New dialogs, not embedded inside stock, receiving, reservation, or planning forms.
- Use wrapper-backed writes for product create/update, variation create/update, stock posting, lot creation, receiving, purchase-order state, reservations, allocations, reorder rules, and reports/actions with business behavior.
- Toasts and alerts must be semantic and user-facing. Do not expose `ex.Message`, SQL/provider details, stack traces, tokens, PII, or raw `result.Message` directly.
- Use typed BlazorBlueprint controls for numbers, money, dates, booleans, and option lists. Avoid native `<input>`/`<select>` where Blueprint controls fit.

## EF, Migrations, And Schema

- EF entities and configurations live in `Inventario.Domain.Shared`.
- Keep all module-owned tables in the `Inventario` schema.
- Index tenant-scoped query paths by `TenantId` first where practical.
- Keep stock-balance uniqueness aligned with product, optional variant, warehouse, location, and optional lot identity.
- Keep movement idempotency and duplicate-prevention logic aware of `ProductVariationId`.
- Generate migrations through the shared migration runner flow. Review generated migrations for destructive drops, missing schema names, missing indexes, and snapshot drift before committing.
- Ensure the migration runner continues to load `Inventario.Domain.Shared`.

## Deployment Rules

- The Docker Compose service name is `inventario`.
- The xeon-dev exposed port is `${INVENTARIO_EXPOSE_PORT:-8105}` mapped to container port `8080`.
- The health check is `http://localhost:8080/health/live` inside the container.
- Inventario depends on Postgres and Bolt Hub in the shared compose stack.
- Docker runtime settings live in `Inventario.Api/appsettings.Docker.json` and the root `docker-compose.yml` service definition.
- If ports, health checks, compose dependencies, Bolt client settings, or deployment workflow behavior change, update this guide and verify xeon-dev deployment.

## Testing Expectations

- Add or update unit/service tests for business rules, validators, idempotency, tenant validation, variant matching, stock math, reservation allocation, and purchasing/receiving transitions.
- Add or update `Inventario.IntegrationTests` for PostgreSQL mappings, migrations, service wrappers, remote `IDataContext`, feature gates, and deployed-shape behavior.
- Add or update Portal contract tests when Inventario UI write paths, grids, entity pickers, breadcrumbs, detail navigation, or toast behavior change.
- Run focused wrapper tests when adding or changing `IBoltRequest` contracts:

```powershell
dotnet test src\Tests\Inventario.IntegrationTests\Inventario.IntegrationTests.csproj --filter "TestCategory=Area:Wrappers" -m:1 /nr:false
```

- Build the affected projects for non-documentation changes:

```powershell
dotnet build src\Modules\XFramework.Inventario\Inventario.Api\Inventario.Api.csproj -m:1 /nr:false
dotnet build src\Presentation\XFramework.Portal\XFramework.Portal.csproj -m:1 /nr:false
dotnet build src\Tests\Inventario.IntegrationTests\Inventario.IntegrationTests.csproj -m:1 /nr:false
```

- Documentation-only changes should at least run `git diff --check` and verify referenced paths.

## Do

- Keep Inventario tenant-scoped and inventory-focused.
- Use Inventario services and wrapper contracts for business behavior.
- Preserve product/variant/lot/warehouse/location consistency through every stock workflow.
- Keep product-level views aggregated and variant-specific views filtered.
- Use `BbDataGrid` and `XfEntityPicker` for Inventario Portal workflows.
- Update this guide when a bug fix or feature change teaches future agents a new rule.

## Do Not

- Do not bypass `IInventarioServiceWrapper` for stock, lot, reservation, purchasing, receiving, planning, variation, or product business writes from Portal.
- Do not directly mutate stock balances without movement/transaction records.
- Do not allow a lot, reservation, receiving line, or stock movement to silently cross product or variant boundaries.
- Do not expose raw GUIDs as primary labels in Portal.
- Do not create raw HTML tables or custom table components for Inventario list/report UI.
- Do not duplicate product detail navigation when the shell/sidebar already owns it.
- Do not leave this guide stale after Inventario bug fixes, feature changes, integration contract changes, deployment changes, or UI workflow changes.
