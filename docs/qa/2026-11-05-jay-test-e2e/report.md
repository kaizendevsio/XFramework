# Jay Test 01 Inventario and POS E2E

Follow-up 2026-11-07: Location Detail is fixed and deployed; cash checkout, partial/full refunds, reconciliation and hold/resume/cancel now pass after creating test prerequisites. See the [retest report](../2026-11-07-jay-pos-location-retest/report.md) for evidence, remaining UI findings and coverage limits. The historical results below are preserved.

Date: 2026-11-05/06 (Asia/Singapore). Target: http://xeon-dev:5000. Tenant: Jay Test 01.

Status: Current-configuration browser pass complete; full POS transaction E2E BLOCKED by setup. Real browser against deployed services, signed in as Super Admin. No source-code changes or deployment performed.

Scope: Current tenant configuration, Inventario catalog/basic stock and POS workflows. Existing business records and feature settings are preserved; any synthetic records use QA-JAY-20261105 identifiers. Disabled features and missing setup are recorded as coverage blockers, not passes.

## Workflow Results

| Workflow | Result | Evidence / observations |
| --- | --- | --- |
| Product required-name validation | Pass | Empty Create rejected with Product name is required; form retained. |
| Inline category creation | Pass | New category created from product picker; parent draft retained. |
| Product creation | Pass | Correct USD 12.50 base price, category and catalog enabled; auto-navigation to details. |
| Basic stock receipt | Pass | Added 6 base units using automatic My stock / General stock. |
| Product-scoped variation type and variant | Pass | QA Size / Large created with USD 15.00 price. |
| Variant stock receipt | Pass | Added 4 Large units; distinct base 6 and variant 4 positions; total 10. |
| Adjustment / negative-stock protection | Pass | -7 from base 6 rejected twice. Valid -1 posted (base 5), then +1 restored base 6. Rejection toast is generic. |
| Product movement ledger | Pass | Four entries, correct variants, reasons, deltas and resulting balances. [Ledger](screenshots/product-movement-ledger.png). |
| Warehouse locations/balances/movements | Pass | 1 location, 3 positions, 5 movements including original T-shirt. [Locations](screenshots/warehouse-locations.png), [balances](screenshots/warehouse-balances.png), [movements](screenshots/warehouse-movements.png). |
| Location drill-down | Fail | Reproducible load errors, product IDs instead of names; see ISSUE-001. |
| POS cashier without register | Blocked | No registers; payment and hold actions unavailable. |
| POS register setup | Blocked | No merchant credentials listed; permission to add test prerequisites requested. No settings changed. |
| POS catalog integration | Pass (limited) | Newly created base and Large variant appear; existing catalog-disabled T-shirt absent. Without a register, prices/availability are not resolved, so pricing accuracy is not signed off. |
| POS cart editing / clear | Pass (limited) | Added base + variant, increased base to 2 (3 items), clear confirmation removed both. No stock reserved or payment attempted. |
| Hold without a register | Pass (guard) | Hold rejected with `Select a register.`; no held sale created. [Cart](screenshots/pos-register-required-cart.png). |
| Held-sales list | Pass (empty state) | 0 held sales; search and disabled pagination shown. No resume/cancel transaction tested. |
| Sales / returns lists | Pass (empty states) | Both show 0 records without load errors. [Sales](screenshots/pos-sales-empty.png), [returns](screenshots/pos-returns-empty.png). |
| Return form with no original sale | Pass (guard only) | No returnable lines; Create Return disabled. No refund posted. [Form](screenshots/return-missing-receipt.png). |
| Receipt template previews | Pass (preview only) | Classic, Compact and Bold selected; 80/58 mm selection and merchant-name draft reflected in preview. [58 mm](screenshots/receipt-58mm-preview.png), [Bold](screenshots/receipt-bold-preview.png). No physical print/PDF or saved-template transaction verified. |
| Receipt draft discard | Pass | Restored Classic / 80 mm / blank merchant name and saved footer. |
| Table-layout draft | Pass (editor only) | New layout, table QA1, position column 4 / row 3. Discard restored No table layouts. [Draft](screenshots/table-layout-preview.png). No table selection at checkout tested. |
| Checkout-field draft | Pass (editor only) | Added default field and toggled required; discarded without Save. [Draft](screenshots/checkout-field-draft.png). Runtime validation remains untested. |
| POS reporting guard | Pass (guard); reports blocked | Explicit `POS Reporting is not enabled for this tenant.` [Evidence](screenshots/pos-reporting-disabled.png). |
| Brands list / search | Pass (read only) | Existing NIKE visible under Clothes; unmatched query shows No matching brands. No merge of existing business records attempted. |
| Inventory sales transactions | Pass (empty state) | Date-range page loads; no sold products in range. No sale aggregation verified because tenant has no POS sales. |
| Planning / inventory reporting guards | Pass (guards); features blocked | Direct navigation explicitly says each feature is disabled, rather than a generic load failure. |
| Inventory settings | Pass (read only) | Basic mode, USD, threshold 5, default My stock / General stock; no unsaved changes. |
| Browser errors | No uncaught JS errors observed | `errors` empty; captured console output contained Blazor connection information, not client exceptions. Does not rule out server failures (ISSUE-001). |

## Findings

Missing POS setup is a coverage blocker, not a verified application bug. See [empty register list](screenshots/no-register.png).

### ISSUE-001: Location detail partially fails in Basic tenant

Severity: Medium. Route: `/inventario/locations/1bf50d9c-9f19-4e5e-b7ee-f4fa2e929841`.

Expected: General stock loads without errors and identifies products/variants accurately, as its parent warehouse does.

Actual: `Could not load location.` appears twice on navigation and again on Refresh. Balance rows show truncated product IDs (`2609c06c`, `2fc5816e`) rather than names. Both QA variant/base positions share the same ID without distinguishing them. Totals are present, so the page looks partially usable despite failed loading. Root cause not established by this black-box test.

1. In Jay Test 01, open My stock warehouse and Locations: [before drill-down](screenshots/issue-001-before-drilldown.png).
2. Click STOCK / General stock: [load failure](screenshots/issue-001-location-error.png).
3. Click Refresh: [repeated failure](screenshots/issue-001-refresh-error.png). Reproduced on two separate navigations and refreshes.

Repro video: unavailable. Browser recording was attempted but encoding failed because ffmpeg is unavailable. The earlier `issue-001-step-*`/`issue-001-result` images are an unsuccessful automation attempt, not defect evidence; use the three screenshots above.

## Workflow Improvements

- Explain rejected stock movements. Overdrawing a position is prevented, but the toast only says `Could not post stock movement.` State the quantity available and why the request is invalid. [Actual rejection](screenshots/overdraw-validation-result.png). The earlier `overdraw-rejected.png` predates the actual submission and is not rejection evidence.
- Guide first-time POS setup. Cashier has no register, but product buttons still allow a cart. After adding items the Register picker becomes disabled while Hold tells the cashier to select a register. Prevent this entry path, or allow initial register selection and provide a direct setup action. Clearing the cart recovers the UI; no payment/data corruption observed.
- Collapse the main sidebar on phone-sized screens. At 390 x 844 the expanded sidebar uses a substantial part of the viewport, clipping navigation labels and narrowing search/product content. [Mobile cashier](screenshots/pos-mobile-cashier.png). Held-sales dialog itself fits: [dialog](screenshots/pos-mobile.png). Full mobile checkout is untested.

## Blocked Coverage and Next Verification

This is NOT a full POS sign-off. The following need a configured test register and payment accounts in Jay Test 01:

1. Register persistence, selecting its warehouse/location, register-scoped stock and base/variant prices.
2. Mixed-item checkout, insufficient cash, exact cash/change, discounts/tax, sale idempotency and payment/stock reconciliation.
3. Hold/resume/pay/cancel, reload recovery, table-required holding/checkout and overselling protection.
4. Actual receipt print/PDF, logo and saved-template application.
5. Partial/full returns, duplicate refund prevention, original payment-account refund routing and stock restoration.
6. Customer-wallet checkout and insufficient wallet balance.

Observed blockers: register list empty; Merchant Credential picker has no selectable entry; POS Reporting disabled. Cash-drawer wallet / wallet-type suitability has not yet been established because no register was created. Additional dependency flags may be required; do not assume fixing the register alone completes setup.

Permission to create a labelled test register and required merchant/wallet records and enable missing POS prerequisites was requested, but not received during this pass. No feature flags or saved POS settings were changed.

Inventory optional capabilities (Planning, Reporting, Purchasing, Traceability/lots, Reservations and Fulfillment) remain outside transaction coverage because they are disabled in this tenant. Negative stock is disabled; its rejection guard was tested. Brand merge, destructive catalog deletion, advanced transfers, purchasing/receiving and multi-user concurrency were not tested.

Recommended next step: fix ISSUE-001, approve/provision isolated POS test prerequisites, then run the transaction workflows above against these QA products. No external gateway is required to start verifying the application's cash-drawer workflow; real card settlement is a separate test.

## Test Records and Cleanup

All synthetic records use QA-JAY-20261105. Existing T-shirt and its 10 units are untouched.

- Category: QA-JAY-20261105 Apparel.
- Product: QA-JAY-20261105 Shirt, SKU QA-JAY-20261105, ID 2fc5816e-9f46-4834-87d8-b4435d5d5cb9.
- Product-scoped variation type: QA Size, code QA-JAY-SIZE; variant: Large.
- Stock receipts: base +6 and Large +4, with QA-labelled reasons.
- Base adjustments: -1 then +1, restoring base stock to 6. Rejected overdraw attempts created no ledger entries.
- Records are retained and clearly labelled for blocked POS tests. QA product remains catalog-enabled, with base 6 and Large 4 units; original T-shirt remains 10 units. No stock deletion or historical ledger rewrites were performed.
- Cashier cart cleared; no held sales, completed sales, refunds or wallet transactions created.
- POS receipt, table-layout and checkout-field drafts discarded. No saved tenant settings changed.
