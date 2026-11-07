# POS and Inventario Verification - 2026-10-29

Status at original verification: three defects and explicit coverage gaps. **Follow-up: B01, B02 and B03 are fixed, deployed and retested; see [fix and retest results](fix-retest.md).** The findings below preserve the original evidence. Not a production release sign-off.

## Conclusion

The exercised cash/wallet sales, stock reservation/fulfillment, combination pricing, discount/tax calculation, and refunds behaved consistently. The daily POS report reconciles exactly with this run. No duplicate debit, duplicate refund, or overselling was observed in the bounded scenarios below.

Confidence is reasonable for these core workflows, but not for the entire product. Brand merging is blocked by its confirmation UI, expired stock is incorrectly advertised as sellable, and receipt settings do not visibly reset when discarded. Restricted-user tenant isolation, real payment-gateway settlement, backend-crash recovery, PDF artifacts, and physical hardware remain unverified.

## Scope and Environment

- Target: http://xeon-dev:5000, Compose project `xframework`.
- Deployed release directory: `39825885286-1`.
- Images: Portal `4cbb2b58109f`, POS `96076a6d14a7`, Inventario `3c51fa3eb701`.
- Method: Live browser workflows with synthetic QA data; no mocked responses.
- No deployments, database resets, real payments, or modifications to business records.
- Historical October 22 passes are not counted as fresh passes.

## Verification Matrix

| Area | Status | Evidence / boundary |
| --- | --- | --- |
| Customer wallet checkout / insufficient funds | PASS | Zero balance rejected with `Insufficient balance`, no debit. Funded with synthetic 50; 12.50 wallet payment completed, balance 37.50; full refund restored 50. |
| Last-unit contention / duplicate checkout | PASS (bounded) | Two prepared cashier tabs submitted within the same second. Exactly one completed and one InventoryReservationFailed; on hand/reserved/available all zero. Not a multi-user load test. |
| Interrupted checkout recovery | PARTIAL | Held cart survived reload, including table and variant price. Reload immediately after confirmation left exactly one completed sale and one stock decrement. A backend crash mid-settlement has not been injected. |
| Discounts / taxes / return limits | PASS (bounded) | Excessive discount disables payment. 17.25 minus 2.25 charged 15.00; 20.00 tender gave 5.00 change. Two units: 34.50 minus 4.50 discount, plus 12% tax = 33.60. Two successive partial returns refunded 16.80 each. Full-return quantity cannot be increased again. |
| Brand merge / reassignment | FAIL | Category-filtered list and brand creation work; confirmation is behind the disabled source modal. Merge not applied. B02. |
| Variant-combination pricing | PASS | Product-local roast + tenant-wide size combination saved at absolute 17.25 (base 10, delta 7.25), stocked independently, sold at 17.25 before discount. |
| Warehouse transfers | BLOCKED (UI coverage) | Transfer is disabled in Post Stock Movement, with source/destination explanation. No transfer action found in inspected product/warehouse surfaces. Backend transfer capability was not tested. |
| Lot expiry | PARTIAL / FAIL | Expired lot was correctly rejected at reservation, but catalog advertised its quantity as available. B01. |
| Restricted-user / cross-tenant isolation | NOT VERIFIED | Available authenticated browser profile was superadmin. No restricted-user denial or cross-tenant object-access test was completed. Anonymous cashier and product-stock URLs redirect to sign-in; that alone does not prove tenant isolation. |
| Receipt templates / PDF output | PARTIAL | Compact sale preview and Classic/Bold settings previews render; 58/80 mm choices present. Printable sale DOM specifies 80mm x 300mm. No exported PDF or physical print validated. Discard UI defect B03. Logo upload not tested. |
| Planning / inventory overview | PASS (read/navigation) | Planning loads one low-stock suggestion; overview Low stock 1 opens Planning. Overview reflects the new shipments and returns. Draft PO creation not rerun. |
| POS reports / share link | PASS (bounded) | Today shows four completed sales totaling 71.10, four refunds totaling 61.10, net 10.00. Failed sales excluded from revenue. Share opens a dialog, scoped URL includes tenant/date, Copy link reports Copied. Clipboard contents not independently read back. |
| Physical scanners and printers | Not run | Physical devices are not available to this browser test. |

## Findings

### B01 - Medium: Expired-only stock is advertised as sellable in POS

1. For a new product with zero untracked base stock, create an Available lot with expiry October 27, 2026 (before the October 28 UTC test).
2. Receive one base unit into that lot in the register's warehouse.
3. Open the cashier. The base product appears with `1 available` and can be added to the cart.
4. Pay with exact cash and a table. Reservation fails with `Insufficient allocatable stock`.

Expected: Catalog availability and eligibility should exclude expired lots (or clearly distinguish physically on hand from sellable quantity). Actual: cashier promises availability that checkout cannot fulfill. Backend correctly prevented sale; no expired unit was shipped.

Evidence: [lot expiry](screenshots/expired-lot.png), [cashier failure](screenshots/expired-stock-cashier.png). Failed receipt `POS-20261028165014-cd343a6a47e`; receiving reference `QA-20261029-EXPIRED`, lot `QA-EXPIRED-20261029` on product `8a242667-c503-41a9-b6c2-6accb1b56bb6`.

### B02 - Medium: Brand merge confirmation is hidden behind its parent modal

1. Assign the new QA product the brand `QA MERGE SOURCE20261029` in category QA Beverages.
2. Open Brands, select that category, and use the source row's Merge action.
3. Select `QA ROASTERS` as destination and click Review merge.
4. The original form becomes disabled. The confirmation is not visible or normally clickable.

DOM inspection found both the confirmation alertdialog and original dialog at z-index 50, with the original dialog later in stacking order. The confirmation asks to change one product, but is covered by the source form. No forced click was used, and the merge was not applied. Reload exits the blocked modal.

Expected: The confirmation must appear above its parent with usable confirm/cancel controls. Evidence: [blocked merge](screenshots/brand-merge-stuck.png).

### B03 - Low: Discard does not reset visible receipt settings

1. Open POS Settings > Receipts. Saved values are Compact / 80 mm.
2. Change template to Bold (also reproduced after changing width to 58 mm).
3. Click Discard changes. The edited choice and preview remain visible, including after waiting and clicking again with the button scrolled into view.
4. Reload the page and reopen Receipts. The saved Compact / 80 mm values return.

Expected: Discard immediately restores controls and preview to saved values. Persistence was not changed; the defect is stale editing UI. Evidence: [after discard](screenshots/receipt-discard-stale.png).

## Workflow Improvements and Remaining Gates

- Align cashier sellable availability with reservation eligibility, including expiry. Distinguish physical on-hand quantity from sellable quantity where necessary.
- Expose a clear source/destination transfer workflow, or link to the supported transfer screen. A disabled movement type is not an end-to-end transfer workflow.
- Excessive discounts are correctly rejected, but the preview briefly shows a negative total/change. Show an explicit validation error without misleading payable amounts.
- Before production sign-off, exercise a restricted cashier/inventory account against another tenant's object IDs and privileged endpoints. A superadmin session cannot establish authorization isolation.
- Run a backend interruption during payment/stock settlement in an isolated disposable stack, then verify recovery and idempotency. Browser reload is a narrower test.
- Validate actual receipt PDFs (short/long item lists, 58/80 mm, logo) and physical printer/scanner output. Current evidence covers browser preview/print markup only.
- Real gateway settlement still requires a configured supported provider and sandbox account. Cash-drawer and internal-wallet passes are not gateway settlement evidence.
- No new capabilities or app fixes were implemented during this verification-only run.

## Test Data

- Existing isolated tenant `QA Modules 20261022`; no business tenant modified.
- Local test date October 29 (Asia/Singapore); server receipts use October 28 UTC.
- Synthetic 50.00 funding: `QA-20261029-WALLET-SEED`, checker wallet `704518125172`.
- Insufficient-funds receipt: `POS-20261028163807-9cb3bf82449`, PaymentFailed.
- Successful wallet receipt: `POS-20261028163917-572e5fcf9ec`, 12.50. Rapid double-click on Confirm produced exactly one completed receipt and one debit.
- Full wallet return: `RET-20261028163951-b65a3c5ab5a`, 12.50. Rapid double-click produced one return and one credit. Clicking quantity + above the purchased quantity remained clamped to 1 (UI boundary only).
- [Wallet ledger](screenshots/wallet-ledger.png): 50.00 funding -> 37.50 after payment -> 50.00 after return; merchant wallet returned to 125.00.
- New product `QA Integrity 20261029`, SKU `QA-INTEGRITY-20261029`, ID `8a242667-c503-41a9-b6c2-6accb1b56bb6`; base price 10.00. Added one base unit for contention; sold exactly once.
- Last-unit receipts: completed `POS-20261028164421-2896c37243f`, rejected `POS-20261028164421-cff2283907f`. Evidence: [completed](screenshots/last-unit-completed.png), [rejected](screenshots/last-unit-rejected.png), [final stock](screenshots/last-unit-stock.png).
- Combination `Large Dark QA`: QA Size=Large, product-local QA Roast 20261029=Dark, 17.25. Added 3 units. Held cart `CART-20261028164624-4c56c299948c` required table QA-T1 and survived reload.
- Discounted sale `POS-20261028164708-4995a047a0b`, full ID `4995a047-a0bf-40c9-b540-686e46fab65c`. Reload immediately after confirmation; sale completed once and combination stock went 3->2 with 0 reserved.
- [Receipt preview](screenshots/receipt-preview.png) matches sale, table, discount, tender and change. Printable receipt DOM specifies 80mm x 300mm page, zero margin; physical printing remains unverified.
- Discounted full return `RET-20261028164811-7bb1035bb18` refunded 15.00 and restored the unit. [Fully returned quantity limit](screenshots/return-limit.png).
- Taxed sale `POS-20261028165635-5e8f7764ff2`, full ID `5e8f7764-ff24-4d6b-9802-bf94f205a565`: two combination units, subtotal 34.50, discount 4.50, tax 3.60, paid 33.60.
- Partial refunds `RET-20261028165712-cd7483825a9` and `RET-20261028165756-d03d8549bef`: 16.80 each, both Completed. Second refund retained original tax allocation after current tax settings were reset. [Partial preview](screenshots/tax-partial-return.png), [completed refunds](screenshots/tax-refunds-completed.png).
- [Final product stock](screenshots/final-stock.png): combination 3, expired base lot 1, untracked base 0; reserved 0 throughout final state. Existing QA Coffee restored to base 22 / Large 8.
- [Daily POS report](screenshots/pos-daily-report.png): 71.10 gross - 61.10 refunds = 10.00 net; matches the one non-returned base-unit contention sale.
- [Planning](screenshots/planning.png), [Classic preview](screenshots/receipt-classic.png), [Bold preview](screenshots/receipt-bold.png), [58 mm preview](screenshots/receipt-58mm.png).

## Cleanup and Limitations

- Temporary Finance tax configuration restored to disabled / 0%, then reloaded and verified.
- Receipt-template edits were never saved. Reload confirmed original Compact / 80 mm configuration.
- Synthetic product, variant, lot, brand, funding, sales, failures and returns remain in the isolated QA tenant for reproducibility. They were not deleted or disguised as business activity.
- No application source changes, deployments, restarts, or real external payment transactions were performed.
- This was a desktop browser regression/exploratory pass, not exhaustive coverage of every module endpoint, mobile viewport, authorization permutation, or concurrent load condition.
