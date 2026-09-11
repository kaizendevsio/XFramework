# Inventario acceptance retest

Status: Pending deployment. This checklist is not evidence of a pass.

Target: http://xeon-dev:5000, E2E Inventory POS 20260906. QA task owns live UI acceptance; implementation task owns code/build/service tests. Record deployed revision and test timestamp for every pass. Never apply a pass from a previous deployment to changed behavior without checking the affected area.

## Test data

Use isolated test records prefixed `INV-UX-20260911`. Do not modify the existing E2E Water/Shirt stock or POS records. Record IDs and stock effects here after creation. No QA fixtures have been created yet.

Planned minimal fixtures: one category; one product with base and variant positions; one supplier; one non-default warehouse with two locations; one purchase order with two lines; one lot with known future expiry; one reorder rule. Reuse these across tests. All write tests must remain within the selected tenant. Leave an exact audit of resulting records; do not delete posted stock history.

## Gates

| Gate | Evidence required | Status |
|---|---|---|
| Deployment | Implementation commit, deployed Portal and Inventario image/revision, healthy endpoints, peer coordination preserving POS/Yap | Pending |
| Tenant | Active tenant visibly E2E Inventory POS 20260906 before writes | Pending |
| Focused automated checks | Actual passing test results for receiving identity/quantity, maintenance validation, tenant boundaries, and changed stock semantics | Pending |
| UI acceptance | Screenshots and observed results below, including successful write/readback where applicable | Pending |
| Scope | Implementation diff confined to Inventario and justified coordinated shared-shell changes | Pending |

## Cases

| ID | Finding | Steps and expected result | Status / evidence |
|---|---|---|---|
| QA-01 | INV-01 | Stock opens with one primary table. Switch Balances/Movements without scrolling past another table. Return preserves each view's filters and page. | Pending |
| QA-02 | INV-01/02 | Search product and select warehouse. Verify only matching balances/movements and clear controls. Open balance, return, retain scope. | Pending |
| QA-03 | INV-02 | Existing shirt base 7 and Medium 5 are visibly distinct in global, product, warehouse, reservation, and detail contexts. Lot identity remains visible. | Pending |
| QA-04 | INV-02 | Inspect Reservation, Release, Receipt, Shipment movement types. Labels explain affected quantity dimension; displayed arithmetic and on-hand/reserved semantics are correct. | Pending |
| QA-05 | INV-03 | Global Post Movement requires deliberate product choice. Contextual posting preselects the exact position, including variant/lot/location. Show current balance and quantity effect. | Pending |
| QA-06 | INV-03 | Zero/invalid adjustment and missing required context show local validation without changing stock. Valid isolated posting updates intended position once. | Pending |
| QA-07 | INV-04 | Products search/clear works. Catalog-enabled status cannot be mistaken for positive inventory. Prices show SGD; quantities retain supported precision. | Pending |
| QA-08 | INV-04 | Product summary links/counts reach scoped stock, lots, rules, variations; clear return to module exists. Empty related pages explain what to do. | Pending |
| QA-09 | INV-04/09 | Edit an existing isolated reorder rule, reload and verify. Disable/re-enable it and verify suggestion inclusion changes appropriately. | Pending |
| QA-10 | INV-05 | Category search finds fixture. Category detail opens its product through an explicit accessible action. Back returns to previous scope. | Pending |
| QA-11 | INV-06 | Warehouse detail uses manageable views for summary/locations/stock/movements. Exact stock identities and related detail links work. | Pending |
| QA-12 | INV-06 | Edit fixture warehouse description and location name/type/pickability, save and reload. Existing stock associations remain intact; invalid values are rejected. | Pending |
| QA-13 | INV-07 | Lot status distinguishes eligibility from depleted quantity. No-expiry value is explicit. Search/status/expiry controls work. | Pending |
| QA-14 | INV-07 | Edit supported lot metadata/status on fixture and verify persistence/effect. Navigate product, stock and movement context without raw IDs as sole link cues. | Pending |
| QA-15 | INV-08 | Reservation form shows availability, selected variant and clear expiry allocation behavior. Reject over-allocation and invalid quantity. | Pending |
| QA-16 | INV-08 | Reserve isolated quantity: on-hand unchanged, reserved rises, available falls. Release it: reserved/available restored. Verify status/history and active filter. | Pending |
| QA-17 | INV-08 | Fulfill a separate isolated reservation and verify stock effect and final status. Terminal records cannot offer invalid next actions. Exercise due-expiry only on isolated due record. | Pending |
| QA-18 | INV-09 | Planning suggestion explains quantity. Start PO draft and verify product/variant/supplier/quantity preserved before order creation. Cancelling creates nothing. | Pending |
| QA-19 | INV-10 | Find/open/edit isolated supplier, save/reload; lifecycle controls and related-order navigation work. | Pending |
| QA-20 | INV-11 | Create two-line fixture PO with deliberate products/variants, units/costs. Line and order totals match and show currency. | Pending |
| QA-21 | INV-11 | Open PO offers valid next actions and Receive remaining. Outstanding/overdue filters distinguish states. Supported edit/close/cancel obey actual lifecycle constraints. | Pending |
| QA-22 | INV-12 | Repeat original open-water PO-line selection without posting. Product is water, not audit product. Changing PO/line resets incompatible product/variant/lot values. | Pending |
| QA-23 | INV-12 | Receive part of fixture PO. Product/variant/unit/cost come from the line; remaining quantity is visible. Oversized or mismatched receipt rejected. Valid receipt updates PO and stock once. | Pending |
| QA-24 | INV-12 | Complete remaining receipt; order state becomes complete according to workflow and cannot receive already-completed quantity. Inspect receipt links to PO/product/lot/balance. | Pending |
| QA-25 | INV-12 | Ad-hoc receipt allows deliberate selection and clearly separates untracked, existing lot, new lot. Manufacture/expiry validation works. Form header/footer remain reachable. | Pending |
| QA-26 | INV-13 | Reports show compact near-expiry/expired views. Apply/reset product/warehouse/location/day-window filters. Invalid window rejected; applied scope clear. | Pending |
| QA-27 | INV-13 | With fixture risk stock, report rows match scope and open exact lot/stock. Export, if implemented, matches filters and displayed data. Preserve helpful empty states. | Pending |
| QA-28 | INV-14 | Global/product Sales Transactions explain their source and offer a working Inventario Stock link. Populated filtering verified if supported fixture source exists; no POS edits. | Pending |
| QA-29 | INV-15 | Settings explicitly state actual behavior. Configured SGD appears in all changed money fields/displays and accessible labels. Validate currency and threshold, save/readback and restore test settings. | Pending |
| QA-30 | INV-15 | Unsupported policy settings are clearly identified; no misleading apparent enforcement. Existing negative-stock rules continue to pass implementation tests. | Pending |
| QA-31 | INV-16 | At 1280x800, essential toolbar/table actions reachable; main workflows avoid stacked-grid scrolling. At 390x844, navigation doesn't permanently consume content width. | Pending |
| QA-32 | INV-16 | Keyboard reaches navigation, filters, tabs, row actions and form footer; focus remains visible and returns logically after dialog close. No inaccessible nested button behavior. | Pending |
| QA-33 | Cross-page | Refresh/data load/error/empty states remain distinct, forms show save outcome, dirty state handled, and no relevant console errors during changed workflows. | Pending |
| QA-34 | INV-03 | Open movement, choose Shipment/negative override/reason, cancel, then use another balance's Adjust action. New dialog must represent an adjustment with fresh per-post override/reason/reference. | Pending |
| QA-35 | INV-12 | Ad-hoc receiving: choose base-product lot, then change variant. Incompatible lot selection clears; PO line labels distinguish base/variant when product and remaining quantity match. | Pending |
| QA-36 | Maintenance | Save each new warehouse/location/lot/supplier/rule update against PostgreSQL, then repeat with a stale stamp. First save succeeds and persists; stale edit conflicts without overwriting. | Pending |
| QA-37 | Maintenance | Invalid field lengths return useful validation (country code, address, contact/email/phone, supplier reference) rather than a generic DB save failure. | Pending |

## Current coordination

- Initial investigation completed and 16 findings handed to implementation task `01a08ffc-3668-7910-a2b3-512eebad1412` using GPT-6 Astra/high.
- Implementation task is active; it reports a receiving form binding fix underway and backend mismatch rejection already present.
- On the next goal turn, Chrome new and original tabs redirected to sign-in. Autocomplete did not appear on focus. POS QA task has been asked whether shared authentication testing is underway. This is a temporary UI-test dependency, not a reason to stop implementation.
- No fixes have passed QA yet.
- Read-only WIP review identified stale contextual-adjustment fields and a receiving variant change that does not clear lot selection. Both sent to implementation task; QA-34/35 added. Existing purchasing service request hashes reject changed-body idempotency replay; no silent replay defect found in that check.
- Remote Docker read-only check showed Portal, Inventario, IdentityServer, Bolt and Communications healthy. Deployment owner explicitly requested QA remain paused until final combined release health checks complete; health alone is not release completion.
- Review of new maintenance updates found detached entities receive a new ConcurrencyStamp before DataContext.Update attaches them. ServerQuery uses AsNoTracking; ServerDataContext.Update attaches via DbContext.Update; Inventario config marks the stamp as concurrency token. Sent P1 finding to implementation: attach with old token before mutating, then verify success/stale updates against PostgreSQL (QA-36). This is a source-backed finding pending runtime confirmation, not a claimed failed live save.
- Update-field validators requested to match persisted field lengths and produce actionable errors (QA-37).
- Shared deployment run 34588224189 completed successfully. Run 34588648781 remained in progress at the latest authoritative GitHub Actions check. No deployment was restarted by QA.

- Implementation reports all five maintenance PostgreSQL success/invalid/stale/cross-tenant tests passed on first run. Portal build passes. Three outdated UI contract assertions are being corrected; receipt mismatch/replay tests added. Parent source review confirms contextual Adjust resets and receiving variant changes clear lot fields. These are intermediate code/test observations, not deployed UI passes. Authentication appears restored in implementer-owned Chrome tab; owner release pause still applies.

- Deployment owner cleared QA after run34588648781 deployed041bc347, all13apps healthy and Portal ready at10:44:43UTC. Inventario changes are not yet in this release. Parent authenticated in dedicated Chrome tab600257085; active tenant changed to POS QA20260911 without parent action. Requested tenant-switch coordination before any fixture writes. No fixtures created.

## Isolated write sequence (planned, not executed)

Use the prefix above for a category, product, supplier, warehouse, and two locations. Product has base and one clearly named variant; all observations record actual generated IDs.

1. Create a PO for base quantity 10 and variant quantity 8, unit cost SGD 2.50. Expected line totals SGD 25.00 and SGD 20.00; order total SGD 45.00.
2. Receive base 4 and variant 3 into the fixture location, with a future-expiry lot where supported. Expect PO remaining 6 and 5; stock 4 and 3. Confirm an excessive receipt is rejected without changing either balance.
3. Receive the remaining base 6 and variant 5. Expect stock 10 and 8 and no remaining receivable quantity.
4. Reserve base 2. Expect base on-hand 10, reserved 2, available 8. Release it. Expect base 10/0/10.
5. Reserve and fulfill variant 1. Expect variant on-hand 7, reserved 0, available 7. Confirm historical status and movement dimension labels.
6. Use the fixture for metadata edits and reorder-rule maintenance. Deactivate/re-enable only the fixture records. Cancel a suggested PO draft before persistence when testing prefill; create another only if necessary to verify a separate lifecycle.
7. Run responsive/keyboard checks with real rows and an open receipt dialog. Record screenshots after each affected deployment.

The POS task confirmed tenant selection uses shared Chrome localStorage. Hold switching until its credential handoff is complete or paused; it will explicitly release the window. Check the visible tenant before every write. No peer-owned records or posted history may be deleted.

- Implementation reports final focused test run35/35 passing and Portal build passing, including five maintenance wrappers and PO product/variant mismatch, corrected partial receiving, and idempotency. Final label/detail polish and PR/deployment coordination remain. Parent has no additional source findings beyond those already sent. Automated gate awaits exact final revision/test artifacts; live UI cases remain Pending.

## Actual fixtures and session

QA now uses isolated agent-browser session `inventario-qa-isolated`, authenticated normally using an existing vault profile. Shared Chrome and its POS credential form remain untouched. The active tenant was visibly E2E Inventory POS 20260906 before the write.

| Created UTC | Record | ID | Effect / evidence |
|---|---|---|---|
| 2026-09-11 10:54:56 | INV-UX-20260911 Category | e752da24-6cac-49c5-9b31-faf14f4e5a69 | Category only, no stock effect. Detail URL/readback and screenshots/fixture-category.png. Created on pre-implementation041bc347 release for later acceptance. |
