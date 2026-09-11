# Inventario workflow implementation

Implemented on `codex/inventario-workflow-ux`, based on develop `041bc347`. Browser acceptance remains pending deployment and parent QA. No database migration or inventory data repair is required.

| Finding | Implementation | Acceptance focus |
|---|---|---|
| INV-01 | Balances/movements tabs, search, warehouse scope; mounted panels retain independent grid state | Switch tabs after filtering/paging |
| INV-02 | Variant/lot identity across balance, warehouse and movement views; reserved versus on-hand labels | Distinguish base and variant stock; explain reservation deltas |
| INV-03 | Deliberate selection, contextual Adjust, clean form resets, available-stock preview and signed adjustment guidance | Reopen contextual/global forms and post isolated adjustment |
| INV-04 | Catalog status, configured currency, linked product summary, module escape, stock tabs, rule maintenance and contextual empty guidance | Existing product/variant/lot actions remain usable |
| INV-05 | Category search and linked products/scoped product counts | Navigate category to product |
| INV-06 | Warehouse locations/balances/movements tabs and wrapper-backed warehouse/location edits | Persist names/address/type/pickability; preserve stock identity |
| INV-07 | Lot eligibility versus depleted stock, explicit no-expiry, quick filters, metadata/status edits and detail tabs | Edit isolated lot; verify reserved-stock eligibility guard |
| INV-08 | Active/due/history filters, allocation identity, deliberate variant selection and available/earliest-expiry guidance | Reserve/release/fulfill isolated base and variant stock |
| INV-09 | Suggestions/rules tabs, formula explanation, prefilled draft purchase order, rule edit/deactivate | Review draft identity/quantity/scope before submission |
| INV-10 | Searchable supplier list, detail/edit/deactivate and related purchase orders | Save contact/status changes and reload |
| INV-11 | Outstanding/overdue filters, deliberate PO lines with variant, currency totals, open/cancel and Receive remaining | Partial/final receipt updates order correctly |
| INV-12 | Selecting PO line fills/locks product, variant, unit and cost; clears incompatible lot fields; remaining quantity/stock preview; stable retry key and curated validation feedback | Partial receipt and retry produce one document and one stock effect |
| INV-13 | Compact filters, risk tabs/counts, applied scope and lot drillthrough | Populated risk row opens correct lot; window/apply/reset |
| INV-14 | Explain Inventario sales history and link stock movement history; product/date filters | No inference that POS movements imply a missing sales projection |
| INV-15 | Tenant currency used by prices/money controls; validation and dirty state; threshold described; unsupported SKU/negative-stock preferences disabled | SGD labels and settings restoration after test |
| INV-16 | Inventario-only mobile drawer, responsive toolbars, table overflow/actions and scrollable dialogs | Actual 1280x800 and 390x844 browser verification |

## Automated evidence

- Inventario API build: passed, zero errors.
- Portal build: passed, zero errors (existing duplicate-import warnings and obsolete confirmation API warning remain).
- 35 focused Inventario integration/contract tests: passed. These include five new update wrappers with real PostgreSQL persistence, stale stamp rejection, foreign-tenant rejection and field limits, plus receiving product/variant mismatch rejection, corrected partial receipt and exact replay idempotency.
- 86 nonbrowser Portal tests: passed, covering architecture, breadcrumbs, identity/attendance/session contracts, reference saves and service behavior.
- `git diff --check`: passed before commit.

The integration suite uses disposable PostgreSQL through the xeon-dev Docker runtime. It does not mutate live tenant stock. Browser QA uses separate isolated fixtures owned by the parent task.

## Contract and scope notes

New update endpoints only edit demonstrated metadata/lifecycle gaps in warehouses, locations, suppliers, lots and reorder rules. Identity/scope fields are immutable and requests carry original concurrency stamps. Existing business services, feature gates, trusted tenant metadata and generated wrappers remain authoritative. Detached entities are attached before field/stamp changes so optimistic concurrency works.

PO receiving already rejected product/variant mismatches server-side; this implementation fixes UI selection and adds regression coverage. Supplier remains optional. PO state actions follow the existing enum; no invented close state or line-edit API. No report export existed to expose. Display currency does not convert stored amounts. SKU-required and negative-stock registry preferences are not enforcement switches; actual service/feature/per-post policies are preserved.

Deployment must use the normal develop pipeline after shared POS/Yap coordination. Completion requires deployed revision/health evidence and parent browser acceptance; builds alone are insufficient.
