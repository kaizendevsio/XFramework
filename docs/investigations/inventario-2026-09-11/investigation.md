# Inventario UI/UX investigation

Date: 2026-09-11. Portal: http://xeon-dev:5000. Active tenant verified: E2E Inventory POS 20260906. Scope: Inventario only. Inspection via authenticated Chrome UI; no source-code assumptions. Initial inspection complete; implementation and retest pending.

## Findings

### INV-01 — Stock workspace forces serial scrolling (P1)
Stock Balances and Movement Ledger are large stacked cards. Six balances occupy most of the desktop viewport before the 34-row ledger starts. Use Balances / Movements tabs, one primary table per view, visible search and warehouse filters, and contextual movement drilldown. Preserve separate paging/filter state. Evidence: screenshots/stock.png, stock.txt. Acceptance: switching views requires no page-length scrolling; all current detail/post actions remain available.

### INV-02 — Stock identity and movement semantics are ambiguous (P1)
Two E2E Shirt rows have the same displayed product/warehouse/location/lot but different balances (7 and 5). The balance detail also omits variant identity. Investigate and show variant/SKU when relevant; do not infer duplicate data. Ledger Delta is nonzero for Reservation/Release while Before/After stay equal, with no explanation of the affected balance dimension. Show whether change affects on-hand or reserved and label Before/After explicitly. Evidence: stock.txt, stock-balance.txt, screenshots/stock-balance.png. Acceptance: users can distinguish stock positions and explain each movement without guessing.

### INV-03 — Movement posting needs clearer intent and context (P2)
Post Movement opens with an unrelated first product preselected, quantity 0, generic Adjustment type, technical append-only/service language, and no visible balance preview. Replace implementation copy with practical stock effects; require deliberate product selection or context from balance, show availability and variant selection, explain adjustment quantity semantics and negative-stock override. Evidence: stock-post-movement.txt and matching screenshot. Submission not performed during initial inspection.

### INV-04 — Product status, navigation, and replenishment actionability (P2)
Products shows Available status for a zero-stock item; distinguish catalog enabled status from inventory availability. Prices lack visible currency and quantities use inconsistent 2/4 decimal formatting. Product detail replaces the whole module sidebar with local navigation; retain a clear module escape and actionable linked summary counts. Product Stock confirms INV-02: the 5-unit position is Size: Medium and the 7-unit position is Base product. It repeats stacked balances/ledger (INV-01). Replenishment has Add Reorder Rule but no visible edit/deactivate action on existing rules. Empty Lots and Sales Transactions say only No results found. Preserve existing variants, lot creation, receipt/posting, and scoped rules; add contextual guidance and rule maintenance if not reachable elsewhere. Evidence: products-loaded, product-shirt, product-stock, product-replenishment, product-variations, product-lots, product-transactions (.txt and screenshots).

### INV-05 — Category lists do not connect to product work (P2)
Category detail lists its products without visible product links/actions; the list lacks a prominent search. Make names/counts navigable to scoped products and retain filters on return. Keep identifiers secondary. Evidence: categories, category-detail.

### INV-06 — Warehouse workspace and maintenance gaps (P1)
Warehouse detail stacks Locations, Stock Balances, and Recent Movements. Balances omit both lot and variant, producing indistinguishable rows for water and shirts. No visible edit warehouse or edit location action; only create location. Provide tabs and contextual links plus supported edit actions for names/address/type/pickability, respecting existing stock constraints. Evidence: warehouses, warehouse-detail. Acceptance: distinguish each position; edit an existing test location and verify persistence; related stock is accessible without scanning three tables.

### INV-07 — Lot traceability and status need operational clarity (P2)
Lot List shows Available for a depleted lot (0 on hand), expiry blank, and lacks visible search/expiry quick filters. Detail stacks balance and ledger, shows technical Source InitialReceipt 11111111 and generic reference types without drillthrough. No visible lot edit or status action. Distinguish lot eligibility from stock status, show No expiry explicitly, add expiry/status filters and supported metadata/status maintenance with clear effects. Evidence: lots, lot-detail.

### INV-08 — Reservations need allocation context and active-work filters (P1)
List of 11 historical reservations omits warehouse/variant/lot identity and shows technical reference types such as POS.SaleLine. No prominent active/expiring/status filter or search. Create form defaults to the first product, quantity zero, FEFO acronym, and no visible available-stock preview or variant picker. Provide clear stock-position selection, explain earliest-expiry allocation, show available amount, and active/expiring views. Preserve reserve/release/fulfill/cancel/expire capabilities. Evidence: reservations, reservation-create. Existing reservations were not changed.

### INV-09 — Planning stops at read-only suggestions (P1)
Planning stacks suggestions and rules. Suggested water quantity 10 has no action to start a purchase order or explanation for how 10 derives from available 20, point 25, max 30 and configured qty 6. Existing rules have no visible edit/deactivate controls here or on product replenishment. Add rule maintenance, concise calculation explanation, and a prefilled draft purchase-order entry point (review before order submission). Keep supplier/product/warehouse context. Evidence: planning, product-replenishment.

### INV-10 — Supplier maintenance is not exposed (P1)
Supplier List offers Create Supplier and Refresh but no view/edit/deactivate action. Clicking the existing row only focuses it. Add searchable supplier maintenance using supported lifecycle rules and links to its purchase orders. Evidence: suppliers, supplier-row-click.

### INV-11 — Purchase-order lifecycle lacks next-step actions (P1)
Open PO detail has only Refresh/back, lines, and a generic empty receiving list. No Receive action, edit/close/cancel action or monetary total. Create PO preselects the unrelated first product, allows no supplier, and lacks visible line/overall totals or variant choice. Provide contextual Receive remaining, supported lifecycle actions, totals with currency, deliberate selection, and outstanding/overdue filters. Do not introduce supplier-required behavior if optional is intentional. Evidence: purchase-orders, purchase-order-detail-loaded, purchase-order-create.

### INV-12 — Receiving PO line can disagree with product (P1, functional)
Repro: Receiving > Receive Stock > Purchase Order PO-20260906104750545 (Open) > PO Line E2E Bottled Water (1.00 remaining). Product remains Audit Viewer Verification 20260907 (AUDIT-VERIFY-20260907), including after selector settles. No submission performed. Selecting a PO line must fill/lock the matching product/variant/unit/cost and show remaining quantity; reject mismatches server-side too. Evidence sequence: screenshots/receiving-create.png, receiving-po-selected.png, receiving-line-selected.png; matching .txt. Also separate receive-against-PO vs ad-hoc receipt, distinguish existing/create/untracked lot, and preview stock effect before posting. No video: connected Chrome interface used after CLI attachment failed.

### INV-13 — Reports need compact, actionable risk views (P2)
Reports already has product/warehouse/location filters and helpful risk-specific empty messages; preserve these. Large filter and stacked near-expiry/expired cards occupy almost a full tall desktop viewport even when empty. Use compact filters and risk tabs/counts, label expiry window in days, expose applied scope, and add scoped export if available. Risk rows should link to lot/stock so the report supports action. Evidence: reports. Populated risk data and export not verified in initial tenant.

### INV-14 — Sales Transactions empty state does not explain its data source (P2)
Inventario Sales Transactions and product Sales Transactions are empty even though Stock ledger has historical POS shipment/return references. This does not prove missing POS ingestion (separate models may be intentional). Clarify what this history contains, link users to Inventario Stock movements, provide date/product filters when populated. Investigate Inventario projection/data contract only; coordinate with POS agents before any cross-module assumption. Evidence: transactions, product-transactions, stock.

### INV-15 — Settings promise unclear behavior and currency is inconsistent (P1)
Settings shows SGD while product, PO, and receiving money inputs announce Amount in US Dollar. Settings description says registry configuration and fields are reserved for future UI defaults, leaving users unsure whether SKU/negative-stock/low-stock settings work. Verify each setting is consumed; use configured currency consistently; add validation and practical help text; distinguish active behavior from unsupported options. Do not silently alter stock policy. Evidence: settings, product-edit, purchase-order-create, receiving-create.

### INV-16 — Laptop and narrow layouts hide critical work (P1)
At 1280x800, Stock needs horizontal scrolling to reach last movement/actions and the second table is below the fold. At 390x844 the persistent sidebar occupies about 160px, leaving a very narrow content strip; the title/breadcrumb wrap, table data and actions disappear offscreen. Provide compact mobile drawer navigation scoped safely to Inventario (coordinate any shared shell change), responsive toolbars, deliberate table overflow with visible scroll affordance, and reachable/sticky row actions. Evidence: screenshots/stock-1280.png, stock-390.png. Viewport restored after inspection.

## Coverage and acceptance checklist

| Page | Inspected | Required verification after implementation |
|---|---|---|
| Products | Catalog, shirt summary/edit, stock, variants, replenishment, empty lots and sales | Search/clear; product detail navigation; explicit status; correct currency; base/variant identity; stock tabs; rule edit |
| Categories | List and Apparel detail | Search; open category; linked product; edit persistence without touching unrelated items |
| Warehouses | List and main warehouse detail | Summary/location/stock/movement views; edit test warehouse/location; lot and variant identity |
| Lots | List and depleted water lot | Expiry/eligibility vs stock status; metadata actions; stock and movement drilldown |
| Stock | List, base-shirt balance detail, post form; 1280 and 390 viewport | Tab state; product/warehouse search; variant identity; movement semantics; contextual posting and validation |
| Reservations | List and reserve form | Status/expiry filter; variant and stock availability; valid reserve/release lifecycle on isolated test item |
| Planning | Suggestions and rules; product-scoped rules | Edit/deactivate rule; explain suggestion; create reviewable PO draft with context |
| Suppliers | List and row interaction | Open/edit supplier; clear status; related orders |
| Purchase Orders | List, open detail, create form | Deliberate product selection; variant/unit/currency; totals; supported lifecycle and Receive remaining |
| Receiving | List; create form; PO and PO-line selection | PO line cannot mismatch product/variant; remaining qty; partial receipt updates balances/order exactly once |
| Reports | Filters and both empty risk states | Filters/reset/window validation; populated lot drillthrough; compact tabs; export if implemented |
| Sales Transactions | Global and product empty views | Explain source; Stock link; date/product filtering for populated data; no POS scope expansion |
| Settings | Read current threshold 6, currency SGD, Require SKU on, negative stock off | Validation, actual consumption, currency consistency, success/dirty state; restore test edits |

## Implementation order and boundaries

1. Fix receiving identity and stock/variant visibility, settings/currency consistency, and responsive usability.
2. Refactor dense multi-table screens into task-oriented tabs, with compact summary counts and persistent filter/paging state. Retain all existing capabilities.
3. Expose missing maintenance actions for existing entities and connect planning > purchase order > receiving using existing APIs first. Add small endpoints only for demonstrated gaps.
4. Polish search, dates (unambiguous locale/timezone), quantities (appropriate precision plus unit), empty states, accessible labels, keyboard focus and errors.

Use the simplest implementation consistent with XFramework conventions. No broad redesign of other modules, new inventory accounting engine, speculative forecasting, or POS edits. Transfer/cycle-count/valuation may be worthwhile future features, but need separate validation of existing backend support and are not mandatory additions for this pass. Shared Portal deployment must preserve concurrent POS/Yap changes. Coordinate deployment ownership before publishing and never deploy an older worktree over their work.

## Evidence and limits

Browser session was already authenticated. Active tenant was explicitly switched from All Tenants to E2E Inventory POS 20260906. Screenshots and full DOM accessibility snapshots are stored alongside this file; recommendations are grounded in visible UI. Source code was not inspected for this investigation. No inventory posting, reservation mutation, purchase-order creation, settings save, or deletion was performed. Create/edit dialogs were cancelled. Some navigations briefly returned old/empty content; loaded captures were used and no transient state is claimed as a confirmed defect. Console warning/error samples returned none. Backend enforcement, role restrictions, populated risk reports, and all write workflows require implementation-stage tests and isolated UI retests. 

16 findings: 10 P1 and 6 P2. P1 means high-priority workflow/identity/accessibility issue; no claim of a proven persisted-data corruption. INV-12 is a confirmed visible mismatch before submission; backend outcome is untested. All other gaps are observable UI limitations or recommendations, subject to verifying supported backend behavior.

## Buddy handoff

Implementation task must use gpt-6-astra with high thinking. Parent QA task: 01a08ff2-8ac8-7b61-9793-036f75bd9b83 (Audit Inventario UI and UX). Read this report and linked evidence, implement in an isolated worktree, run focused checks, coordinate shared deployment, and send parent a concise ready-for-retest message with commit, deployed revision, URLs, findings addressed, and remaining limits. Parent will retest and send concrete follow-up defects. Do not declare completion based only on builds.
