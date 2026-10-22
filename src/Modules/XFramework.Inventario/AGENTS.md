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
- `src/Presentation/XFramework.Portal.Features.Inventario/Pages`: Portal Inventario UI in its feature RCL. Shared pickers live in `XFramework.Portal.Shared/Components`. UI guidance lives in root `rules/UiGuidelines.md`; keep module-specific workflow details here.

## Ownership Boundaries

- Inventario owns only the `Inventario` database schema.
- IdentityServer remains the source of truth for tenants, credentials, roles, and user identity.
- Communications/Notifications own external messages and delivery. Inventario may call approved wrappers for notifications, but must not duplicate messaging infrastructure.
- Wallets owns money movement and wallet ledger behavior. Inventario sale price/cost data is catalog/inventory metadata, not wallet balance data.
- Store `TenantId`, `CredentialId`, and other cross-module IDs as links. Do not add cross-schema foreign keys unless an explicit architecture decision says so.
- All reads and writes must be tenant-scoped. Do not trust client-supplied tenant IDs on protected or business-critical paths when trusted metadata/context exists.

## Domain Rules

- `Product` is the catalog root.
- Brands remain product strings, not catalog entities. `ProductBrand.Normalize` stores uppercase brands with collapsed Unicode whitespace; blank is null. Product create/update enforce it without rewriting other products. `ProductBrandService` derives bounded, distinct suggestions only from the trusted tenant's selected category. Merge preview/confirmation requires tenant-management capability and the catalog feature, an existing category-local target, and the previewed affected count; the scoped atomic update preserves stock/prices and invalidates tenant product caches. Same normalized source/target cleans legacy formatting. Portal uses `ProductBrandPicker` and `ProductBrandMerge` from the Inventario feature RCL; page wiring stays with the owning page.
- `/inventario/brands` scopes selection to one category and hosts the merge dialog. Its management permission comes from the current tenant's `GetInventarioSetup.CanManage`; category/tenant changes clear the draft and stale completions cannot reopen or apply the old scope. It never creates categories or writes products through remote `IDataContext`.
- `ProductVariationType` is the reusable or product-local type/category for variants, such as size, color, or packaging.
- `ProductVariation` is a sellable optional product dimension. `ProductVariationId = null` means base product stock.
- Variant-aware inventory must keep product and variant consistency across lots, balances, movements, reservations, purchase order lines, receiving lines, reorder rules, product transactions, and reports.
- Use absolute variant prices. Preserve legacy `VariationType` and `AdditionalPrice` compatibility only where existing migration/API behavior requires it.
- Explicit combinations remain one `ProductVariation` ID with an absolute price and independently dimensioned stock. `CombinationOptions` stores 2-8 distinct type-ID/value pairs in JSONB; `CombinationKey` is the server-computed order/case/whitespace-normalized SHA-256 key with live tenant/product uniqueness. Every type must be enabled and belong to this tenant and either this product or the tenant-wide scope. Do not accept a single `ProductVariationTypeId` together with options, silently drop options on legacy updates, or bypass the variation wrapper. Combination updates require `ExpectedConcurrencyStamp`. Use `ProductVariation.GetSellingPrice(basePrice)` for legacy display compatibility; zero-priced combinations must remain zero. POS catalog/cart/sale paths already use absolute `Price` and retain the one variant ID through reservations and movements. Deploy the additive combination migration before the updated services; regenerate/deploy the shared MemoryPack contracts and their callers together.
- `Warehouse` contains `InventoryLocation` records. A stock operation must validate that locations belong to the selected warehouse when both are supplied.
- POS register lists must not eagerly query warehouse/location lookups when `inventario.warehousing` is disabled. POS being enabled does not implicitly enable Inventario sub-features; show missing register setup prerequisites without hiding the register list or weakening backend gates.
- Sales transaction filters need their own responsive grid tracks. Do not put product search and two date pickers into the fixed-width `xf-filter-actions` header toolbar; it can overflow left into the sidebar. Global transactions link to the reporting movement view only when reporting is enabled; product transactions link to that product's Stock section.
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
  - `BasicProductStockService` for the bounded `PostBasicProductStock` wrapper: positive additive receipts in explicit Basic mode only. It requires Inventario, stock balances, movements and warehousing gates plus `inventario.movements:create`; it never enables features or changes mode. Missing defaults require tenant management, and new storage also requires `inventario.warehousing:create`. It preserves existing storage, shares setup's tenant transaction lock, and delegates ledger/snapshot writes to `StockPostingService.StageAsync`. Retries reuse the original destination even after defaults change; invalid explicit defaults require Settings repair.
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

### Verified Coverage Boundaries

- Tenant detail's `API gated` badge describes implemented feature enforcement, not completion of an extended inventory subsystem. Purchasing, Traceability, Planning, Reporting, Negative Stock, and reservation Fulfillment are implemented. Legacy built-in descriptions are displayed using the corrected catalog scope; custom stored labels/descriptions are preserved.
- Purchasing has suppliers, purchase orders/status transitions, and idempotent receiving that posts stock and updates order quantities. Planning has reorder rules, low-stock queries, and rule-based reorder suggestions; this is not demand forecasting.
- Traceability has product/variant lots, batch references, expiry, and lot-aware stock/allocation workflows; this is not a serial-number tracking subsystem. Reporting has stock positions, movement ledgers, allocation status, low-stock/expiry reports, and snapshots; do not infer a valuation engine from the catalog description.
- Negative Stock is already enforced by `StockPostingService` for both ordinary posting and transfers. A negative result requires both request `AllowNegativeStock` and the tenant's `inventario.negative_stock` feature; enabling the feature alone is not an override.
- Fulfillment means the existing reservation shipping workflow, not separate pick/pack. `ReservationService.FulfillAsync` checks `inventario.fulfillment` before reading or mutating the reservation; existing reservation route permissions remain unchanged. Fulfillment requires Reservations, and POS Sales declares Fulfillment as a prerequisite. POS checks the flag before continuing checkout and immediately before external payment capture/retry; completed-sale replays remain readable.
- Deploy migration `20261022010000_EnableExistingReservationFulfillment` through the migration runner **before** deploying the new gate. The data-only migration enables missing/disabled legacy Fulfillment placeholders for enabled, non-deleted tenants with enabled, non-deleted Inventario and Reservations. It preserves custom metadata, deleted Fulfillment rows, and all other feature choices; changed tenants receive fresh concurrency stamps. New tenants keep Fulfillment default-off. Explicitly deleted Fulfillment rows require administrator repair before shipping. Rollback preserves grants; services never auto-enable the flag at runtime, so later disablement is respected.
- Evidence: `InventarioPlannedFeatureGateTests` covers service denials before data access and route mappings; `StockPostingServiceTests` covers negative shipment/transfer opt-in and feature combinations. Existing `PurchasingServiceTests`, `InventoryPlanningReportingServiceTests`, and `ReservationServiceTests` exercise workflow success. PostgreSQL/wrapper coverage lives in `PlanningPurchasingReportingTests`, `WarehousingTraceabilityTests`, and `StockReservationTests`; source coverage is not a claim that those integration suites ran in a metadata-only review.
- Fulfillment rollout regression coverage lives in `FulfillmentRolloutMigrationTests` (PostgreSQL cohort/default/tombstone/custom-metadata/stamp checks), `PosReadinessRegressionTests` (disabled checkout/payment retry and completed replay), and `ReservationServiceTests` (disabled no-write and enabled shipping/replay).
- POS's pre-payment fulfillment recheck must run before persisting a new Pending payment or PaymentPending sale. Denial before the first payment attempt leaves reserved inventory cancellable. Never downgrade an existing Pending payment on feature denial: an earlier charge may have succeeded with a lost response, so its existing recovery and cancellation protections must remain intact.

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

### Operational maintenance and Portal workflows

- Warehouse, location, supplier, lot and reorder-rule maintenance uses the corresponding `Update*` wrapper with the original concurrency stamp. Tenant, identity and stock scope remain immutable. Reject stale edits, invalid lengths and invalid date/quantity ranges; do not allow non-pickable locations or ineligible lots while they hold reserved stock.
- `IDataContext` query results are detached. Call `Update(entity)` before changing fields so persistence captures the original concurrency stamp, then assign a new stamp. Cover successful persistence, stale edits and foreign-tenant requests through the real wrapper.
- Stock, warehouse, lot, planning and expiry workspaces use tabs with independent grid state. Preserve variant and lot identity, native filters/sorting, and contextual actions. Reservation/release deltas affect reserved stock; movement before/after snapshots describe on-hand stock.
- Stock entry belongs to product details; `/inventario/stock` is only a legacy redirect to the supplied product's Stock section or the product catalog. Reservation, purchase-order and ad-hoc receipt forms require deliberate product selection. PO receiving fills and locks product, variant, unit and cost from the selected outstanding line. Keep one receipt idempotency key across retries and clear incompatible lot data when identity changes.
- Product Stock stays discoverable when required features are disabled and offers administrators a confirmed feature-enablement action through IdentityServer. Do not silently enable traceability. Basic-mode Add stock uses `PostBasicProductStock`, retaining the exact pending request and idempotency key on uncertain retries. Advanced stock actions and location/lot identity remain available. Product creation uses the returned `ProductResponse.Id` to open details; inline category creation uses the allowlisted category mutation, while product creation remains a wrapper operation.
- Tenant setup and catalog defaults are owned by `InventarioSetup` in the Inventario schema. Read them through `GetInventarioSetup`; it falls back to legacy `Settings:Inventario:*` records only when no setup/preferences row exists. Product warnings consume its low-stock threshold, and money inputs/price labels consume its default currency. Display currency does not convert existing amounts or replace transaction currencies in financial reports. SKU-required and negative-stock registry preferences are not enforcement switches.
- `/inventario` is the read-only module entry gate: administrators of fresh, unconfigured tenants go to `/inventario/setup`; other tenants go to `/inventario/overview` when reporting is enabled, otherwise Products. The real Overview has aggregate charts and bounded recent movements with report/product drilldowns; its sidebar link uses the reporting gate. Products also checks setup status before catalog queries, covering direct entry. Explicit setup access still shows the management restriction. Cancel goes to `/inventario/products?skipSetup=true`; this opt-out skips only the prompt and never authorization/feature checks. Setup requires trusted tenant-management capability and enabled Inventario; Basic also requires warehousing. Confirmation commits storage, preferences, and completion atomically under a per-tenant PostgreSQL lock. Never seed products, stock, wallet balances, or silently change feature flags.
- The Inventario mobile drawer and responsive table/dialog styles are scoped to Inventario. Preserve the shared shell behavior of other modules and coordinate shared Portal deployments with their active QA tasks.

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

- Setup supports a simple stock area without asking small businesses for warehouse codes. `UseSimpleStorage` reuses enabled storage when possible; it creates only the minimum warehouse/location records required by stock accounting when warehousing is enabled, and never enables features implicitly. Basic and Advanced are setup preferences, not destructive data conversions. Mode changes through `UpdateInventarioPreferences` preserve storage IDs, stock, and setup completion.
- New-product availability is a tenant default, applied by `ProductService` only when the create request omits an explicit choice. Existing products are never changed by a preferences update. Default storage selections must be tenant-owned, enabled, and a matching warehouse/pickable-location pair.

- Calendar controls produce dates without a timezone. Portal sends these as UTC midnight without shifting the selected day (`InventoryDisplay.CalendarDateUtc`); services normalize incoming timestamps with `InventoryLotService.NormalizeUtc` before PostgreSQL `timestamptz` persistence and date comparisons. Cover purchasing, receiving lot dates, lot maintenance, and reservation expiry with real PostgreSQL wrapper tests.
- Inventario selects use `InventorySelect` to provide an associated label and human-readable selected text before popup options mount. BlazorBlueprint 3.16 `BbFormFieldSelect` does not forward the required accessible name in this usage.
- `XfEntityPicker` has one native trigger button; do not nest a button inside `BbPopoverTrigger AsChild="false"`. Preserve disabled state and keyboard focus when changing shared picker markup.
- Programmatic Inventario dialogs and mobile navigation capture and restore their keyboard opener through `inventario-focus.js`. Verify Escape focus return and a reachable footer at 390×844; allow grid children to shrink with `min-width: 0` instead of clipping horizontal overflow.
- Mounted tab panels contain hidden grid controls. Browser QA must target the visible panel and verify resulting state; focus plus Enter avoids misleading offscreen click results.
- BlazorBlueprint 3.16 flat-grid filtering also requires `GetSortAndFilterExpression`; its template column omits that projection and falls back to reflection on a property name. Use the feature's `InventoryTemplateColumn` compatibility subclass with matching `SortBy`/`FilterBy` for filterable template cells. It retains the native `BbDataGrid` and column rendering. Test computed lookups through the actual flat-grid pipeline; a direct-property harness does not reproduce this failure.
- Reservations load allocations through `GetReservationAllocations`. Do not request `Take(2000)` through remote `IDataContext`: its materialization limit is 1000. A failed prerequisites load must show an explicit retry state instead of an empty grid and an enabled create action.
- Receiving detail must retain base/variant identity, stored units, and tenant currency with numeric cost formatting. Check date-picker buttons inside the innermost mobile form grid, with dates selected, rather than measuring only the outer dialog.
- Inventario's Escape compatibility handler closes an expanded entity picker, date calendar, or select popup inside its parent dialog through the library's pointer outside-dismiss path. It prevents the separate dialog Escape listener from discarding the draft. Keep this scoped to Inventario dialogs and use fingerprinted host assets so browsers receive updated CSS/JavaScript after deployment.
- Keep planning draft supplier prefill distinct from purchase-order list supplier scope. Reorder rules store a supplier preference as text; resolve only an unambiguous active tenant supplier by exact code/name/display label, and retain the preference in Notes when manual review is needed. Show and clear product/supplier route scopes explicitly.
- Every detail-navigation grid needs a native link or named View action as well as `OnRowClick`; active reservations need View alongside lifecycle commands. Link existing identifiers to their existing detail route and retain native filtering/sorting.
- Carry category, supplier, and product scope through both row-click and explicit View routes, product sidebar section links, and return links. Scope is a GUID query parameter, not ambient state. Show a scope label and clear action; reapply product category filtering when query parameters change.
- Reservation detail shows base/variant identity and current on-hand/reserved/available quantities from its loaded balance, with exact stock-position links. Reservation quantity validation uses the selected eligible stock scope; retain rejected drafts and map known business failures to actionable text without exposing arbitrary backend messages. Product-context movements disable zero quantities and show selected-position availability with signed-adjustment guidance.

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

- Overview low stock counts active reorder-rule positions, not unique products. Use `GetLowStockCount` for the uncapped SQL aggregate and `GetReorderSuggestionPage` for bounded Planning pages; both aggregate all matching balances before filtering/paging rules. Preserve the original legacy list contracts and their 1,000-row bound.
- Planning validates its `warehouseId` query scope against the selected tenant and rejects unavailable scope without broadening it. The paged endpoint requires `inventario.planning:view` on both REST and Bolt. Native supplier filtering runs on the server before count/page calculation; other suggestion columns deliberately do not advertise unsupported filtering or sorting. Rule-grid filters remain native.

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
