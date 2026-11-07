# Jay Test 01 POS and Location Retest

Follow-up 2026-11-07: UI-002 (dialog clipping) and UI-003 (direct sale loading) are now fixed, deployed and retested. See the [targeted UI retest](../2026-11-07-pos-dialog-sale-links/report.md). The findings below preserve the earlier test results.

Date: 2026-11-07 (Asia/Singapore). Environment: http://xeon-dev:5000.
Tenant: Jay Test 01 (`69fa3896-0afe-42fa-a11f-5e265207d9d6`).

## Result

The location-detail bug is fixed, deployed and verified. Cash checkout, partial and remaining-balance refunds, stock restoration, payment-ledger reconciliation, and hold/resume/cancel passed through the deployed browser UI. This is not a full POS sign-off: two additional UI defects remain, and the untested workflows below are not passes.

The [previous report](../2026-11-05-jay-test-e2e/report.md) records the original location failure and missing POS prerequisites. This follow-up resolves that location finding and the setup blockers for cash transactions.

## Location Fix

Root cause: Location Detail queried Reservations even when that feature was disabled. Its failure interrupted the subsequent product-name lookup, leaving partially loaded data and product IDs on screen.

- Query Reservations and Variations only when their features are enabled.
- Hide the Reservations count and tab when disabled.
- Resolve and display base/variant labels in the detail grids.
- On required-read failure, show a persistent retry state instead of partial summary data.

Before enabling POS dependencies, verified General stock with Reservations still disabled: three named stock positions, five movements, no load error and no Reservations tab. Evidence: [balances](screenshots/location-balances-before-reservations.png), [movements](screenshots/location-movements-before-reservations.png).

After the sale and refunds, verified the location again with Reservations enabled: three positions, thirteen movements and two fulfilled reservations. Evidence: [balances](screenshots/location-final-balances.png), [movements](screenshots/location-final-movements.png), [reservations](screenshots/location-final-reservations.png).

Parent warehouse also reconciles to one location, three stock positions and thirteen movements: [warehouse](screenshots/warehouse-final.png).

## Test Prerequisites

Created through the tenant UI; no external gateway or real card settlement was used.

| Record | Value |
| --- | --- |
| Merchant | QA-JAY-20261107 Merchant |
| Merchant identity ID | `e47e5627-f15c-48ff-a4a9-59dbae390e25` |
| Merchant credential | `qa.jay.merchant.20261107` |
| Wallet type | QA-JAY-20261107 USD / QAJAYUSD / USD |
| Cash-drawer wallet | Account `591675905975`, ID `a3074d44-61e7-44f9-ba46-7c2580e881f4` |
| Register | QA-JAY-20261107 Register / QA-JAY-20261107 |
| Warehouse | My stock / STOCK / `41d5c0cc-462b-45da-8e82-4e65ba3b7c14` |
| Location | General stock / STOCK / `1bf50d9c-9f19-4e5e-b7ee-f4fa2e929841` |
| QA product | QA-JAY-20261105 Shirt / `2fc5816e-9f46-4834-87d8-b4435d5d5cb9` |

Enabled only the missing Reservations and Fulfillment dependencies using the tenant module dependency confirmation. Basic inventory mode and saved POS settings were preserved. Planning, Purchasing, Inventory Reporting, Traceability, Negative Stock and POS Reporting remain disabled.

The merchant credential's generated password was not saved in this report. Reset it through administration if an interactive merchant login is needed; these tests used the existing Super Admin session.

## Browser Workflow Results

| Workflow | Result and evidence |
| --- | --- |
| Register setup and persistence | Pass. Correct merchant, wallet, USD and warehouse/location. [Register](screenshots/test-register.png). |
| Register-scoped catalog | Pass. Base USD 12.50 / 6 available; Large USD 15.00 / 4 available. Existing catalog-disabled T-shirt absent. |
| Mixed-item checkout | Pass. One base and one Large total USD 27.50. |
| Insufficient cash | Pass. Zero and USD 20 cannot submit payment. [Guard](screenshots/underpayment-disabled.png). |
| Cash tender and change | Pass. USD 30 tendered, USD 2.50 change. [Payment](screenshots/payment-change.png). |
| Sale completion | Pass. CashDrawer captured USD 27.50, both lines fulfilled. [Sale](screenshots/sale-captured.png). |
| Receipt preview | Pass for on-screen rendering only. Tenant and item content rendered. [Preview](screenshots/receipt-preview.png). |
| Sale stock and payment | Pass. Base 6 to 5; Large 4 to 3; no reserved stock left. Wallet increased from zero to USD 27.50. [Wallet](screenshots/wallet-after-sale.png). |
| Partial refund | Pass. Large quantity 1 refunded USD 15 to OriginalCashDrawer; Large restored to 4, base remained 5; wallet USD 12.50. [Return](screenshots/partial-refund.png), [stock](screenshots/stock-after-partial-refund.png), [wallet](screenshots/wallet-after-partial-refund.png). |
| Already-returned line guard | Pass. Large Remaining 0; increment did not add a return quantity; submission disabled with no other selection. [Guard](screenshots/already-returned-guard.png). |
| Refund remaining line | Pass. Base quantity 1 refunded USD 12.50; base restored to 6. [Return](screenshots/final-refund.png). |
| Fully refunded sale and guard | Pass. Captured USD 27.50, refunded USD 27.50; both lines Remaining 0 and no third return available. [Sale](screenshots/sale-fully-refunded.png), [guard](screenshots/full-return-guard.png). |
| Wallet reconciliation | Pass. Exactly three entries: +27.50, -15.00, -12.50. Final balance zero, no held funds and zero fees. [Balance](screenshots/wallet-final-zero.png), [ledger](screenshots/wallet-ledger-reconciled.png). |
| Hold/resume/cancel | Pass. Held one base item at USD 12.50, resumed the same cart, held again, then cancelled with confirmation. No held sale or cart left; no stock movement added. [Resumed](screenshots/held-sale-resumed.png), [cancelled](screenshots/held-sale-cancelled.png). |

### Transaction References

- Sale: `POS-20261107070835-c550414636c`, ID `c5504146-36c3-450e-9440-58ece752f4a8`, completed 07:08:36 UTC.
- Large return: `RET-20261107071127-90814cf7aed`, ID `90814cf7-aede-414c-8c30-aa226e368e64`, completed 07:11:27 UTC.
- Base return: `RET-20261107071207-e5bd0fa7889`, ID `e5bd0fa7-889e-4c0f-9692-71869f9d3b5f`, completed 07:12:07 UTC.
- Cancelled held cart: `CART-20261107071323-425f3827a214`.

### Final Data State

| Stock position | Before | After sale | After both refunds |
| --- | ---: | ---: | ---: |
| Original T-shirt | 10 | 10 | 10 |
| QA Shirt base | 6 | 5 | 6 |
| QA Shirt Large | 4 | 3 | 4 |
| Cash-drawer balance (USD) | 0.00 | 27.50 | 0.00 |

Final reserved quantity is zero. The two sale reservations are fulfilled. Eight new movements (two reservations, two releases, two shipments and two returns) explain the increase from five to thirteen movements. Original T-shirt stock and its original receipt were not changed.

Test prerequisites and financial audit records are retained for repeat testing; they were not deleted. The held cart was cancelled.

## Remaining UI Findings

### UI-002: Return dialog actions inaccessible on a short viewport

Severity: Medium. At 1260 x 568, the Create POS Return dialog extends beyond the viewport, clipping its header/footer and submission/cancel controls. Scrolling the action into view did not expose the footer. At 1920 x 1080 the controls became accessible and both returns completed.

Reproduce: open the test sale from the Sales list, choose Create Return at 1260 x 568. Expected: bounded modal height with internal scrolling and accessible actions. [Evidence](screenshots/issue-return-dialog-short-viewport.png).

This is a remaining layout bug, not a failed refund operation. Receipt preview also showed short-viewport close-control accessibility trouble; verify both dialog surfaces when addressing sizing.

### UI-003: Direct sale navigation remains on the loading skeleton

Severity: Medium. Hard navigation/new-tab opening of `/pos/sales/c5504146-36c3-450e-9440-58ece752f4a8` remained loading for over a minute. Reproduced in two tabs. Opening Sales and using the row's View action loaded the same sale immediately.

Expected: the direct URL resolves after authentication/tenant context initialization. Root cause is not confirmed. No uncaught browser JavaScript exception was observed; that does not exclude a server/component lifecycle fault. [Evidence](screenshots/issue-sale-direct-navigation.png).

## Automated Regression and Deployment

Focused regression command:

```powershell
dotnet test src/Tests/Portal.E2ETests/Portal.E2ETests.csproj -m:1 /nr:false --filter 'FullyQualifiedName~LocationDetailLoadingTests|FullyQualifiedName~WarehouseDetailLoadingTests|FullyQualifiedName~InventarioGridFilteringTests' --logger 'console;verbosity=minimal'
```

Result: 12 passed, 0 failed, 0 skipped. These are component/contract regressions, separate from the live browser workflows above. Five new location tests cover all Reservations/Variations flag combinations and required-read failure/retry recovery.

Release build of `XFramework.Portal.Features.Inventario.csproj` passed with zero errors and 64 existing warnings.

Deployed only the Inventario feature assembly and PDB in a derived Portal image:

- Image: `xframework-portal:location-fix-20261107`.
- Parent image: `xframework-portal:warehouse-fix-20261105`.
- Image ID: `sha256:6d64db20290b6e60c9a88793d4088fe95ef36de24f2b39248948a0a71075593b`.
- Local/container DLL SHA256 match: `fdefe5800c702e824f36bfe0f63dd41bc0d052d7a3412f497e6e5f87f06fcbcb`.
- Override: `/home/xeon/location-fix-20261107.override.json`.
- Existing release: `/home/github-runner/xframework-deploy/releases/39825885286-1`.
- Container: `xframework-portal`, healthy after deployment and subsequent verification.

No database migration, volume replacement or backend restart. Deployment retains the existing QA and warehouse overrides. Rollback is to recreate Portal with the same Compose chain excluding the final location-fix override. Source changes remain local; no PR merge was performed for this task.

## Coverage Limits

Not tested here: customer-wallet checkout/insufficient wallet funds; discount/tax runtime behavior; concurrent checkout and duplicate API submissions; table-required hold/checkout; paying a resumed held sale; external gateway/card settlement; physical receipt printing/PDF export; disabled optional inventory/POS reporting features.

The UI over-return guards passed. This does not prove server-side concurrency or duplicate-request idempotency. The prior report's other workflow improvements remain open unless explicitly retested above.
