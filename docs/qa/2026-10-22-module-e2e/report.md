# Inventario, POS, and Finance End-to-End QA

Status: Audit completed with failures and blocked coverage. **Not a release sign-off or an exhaustive pass.**

- Target: http://xeon-dev:5000/
- Release: `8bbd1b4ae4bb9acecb21da480d862cb3861ffaac` (PR #615)
- Date: 2026-10-22
- Method: Live browser testing against the deployed application, without mocked responses. Findings are based on observed UI behavior, not source-code speculation.
- Safety: Use uniquely prefixed QA data; do not modify existing business records. No real payment-provider transactions.
- Browser: Chromium via agent-browser, administrator account, desktop 1600x1000 and responsive spot checks at 390x844. Screenshots use the dark theme. No application source was inspected and no application code was changed.

## Executive Summary

**Four confirmed defect groups: three High, one Medium.** Finance operational lists/reports fail broadly; statement generation also fails. Inventario reorder-rule edits terminate the Blazor circuit. POS base-product availability incorrectly includes variant stock.

The core purchase receipt -> stock -> cash sale -> return -> wallet ledger path passed using isolated synthetic data. Two cash sales totalled USD 40.00; a USD 15.00 return produced net sales of USD 25.00 and a final wallet balance of USD 125.00 from a USD 100.00 opening balance. Stock reconciled to 22 base units and 8 Large units.

All listed module navigation surfaces were visited. Functional depth varies: completed workflows, read-only smoke checks, blocked actions, and unexecuted scenarios are explicitly separated below. Finance cannot receive a full functional pass while its operational screens and statement generation are failing. No root cause is inferred from generic UI error messages.

| Priority | Finding | Result |
| --- | --- | --- |
| High | B01: Finance operational reads and reports fail | Blocks reliable operational administration; failures appear as empty/zero data |
| High | B02: Editing a reorder rule crashes the circuit | Changes lost; reload required |
| Medium | B03: Cashier base SKU availability includes variant stock | Misleading sellable quantity; overselling not proven |
| High | B04: Generate Statement fails | Statement view works, generation does not |

## Coverage

Status definitions: **PASS** means the stated action and its result were observed; **SMOKE** means the page/dialog rendered, not that all its actions passed; **FAIL** is a reproduced defect; **BLOCKED** means prerequisite failures prevented completion; **NOT RUN** is not a pass.

### Inventario

| Surface / workflow | Status | Observed result |
| --- | --- | --- |
| New tenant and feature dependencies | PASS with W01 | Created tenant; Fix all repaired four missing prerequisites |
| Basic setup / settings | PASS | Default warehouse/location, USD and threshold saved |
| Product create and details | PASS | Inline category and brand creation; automatic detail navigation; base price shown |
| Variations | PASS, limited | QA Size / Large created; base USD 12.50 and variant USD 15.00 |
| Product stock | PASS | Added base 20 and variant 8; persisted balances and movements |
| Supplier, PO and receiving | PASS, partial receipt | Supplier created; PO 10 units at USD 5; received 4 into new lot; six remained |
| Lots | PASS, limited | QA lot created through receiving and visible in stock |
| Reservations | PASS | Manual quantity-two reservation created and released; POS fulfillment also produced ledger records |
| Movement explanations / validation | PASS with W03 | Descriptions visible; adjustment of -100 against 20 available rejected without changing stock |
| Planning / reorder rule | FAIL B02 | Planning list loads and rule creation works; editing saved rule crashes |
| Overview and reporting | SMOKE | Data/chart surfaces render; no assertion about all report/export modes |
| Sales transactions | PASS, read comparison | POS product sales showed gross base quantity 2 / USD 25 and Large quantity 1 / USD 15; explicitly before refunds |
| Categories, brands, warehouses, suppliers, receiving lists | SMOKE | QA records visible; brand list and merge dialog render, but merge execution not tested |
| Responsive product catalog | SMOKE | Mobile drawer and stacked content render; dense summary cards push the catalog below the first viewport |

### POS

| Surface / workflow | Status | Observed result |
| Register setup | PASS | QA merchant, wallet, warehouse, location and USD register saved |
| Table layout / settings | PASS, limited | QA Dining / QA-T1 saved; required table selection enforced when holding; retained on resume/payment |
| Base and variant cash sale | PASS | USD 27.50 with USD 30 tendered and USD 2.50 change |
| Hold / resume / second payment | PASS | Resumed cart and table; second base sale USD 12.50 |
| Return | PASS | Returned one Large unit for USD 15; inventory restored and wallet debited |
| Catalog availability | FAIL B03 | Base tile counts variant quantity |
| Overview / reports | PASS, limited | Two receipts / one return; gross 40, refunds 15, net 25 |
| Share report | PASS | Dialog opens; Copy link changes to Copied |
| Receipt settings / preview | PASS, limited | Compact / 80mm / merchant name persist; preview shows correct lines, cash and change |
| Template/designer options | SMOKE | Classic, Compact and Bold plus section controls visible; not all templates or edits tested |
| Customer wallet / quick customer | SMOKE | Payment mode and Create customer menu entry visible; no customer-wallet payment completed |
| Mobile cart / payment | PASS, UI only | Added item, reached Pay, opened usable payment dialog, then removed item without payment |
| Physical printing, scanners and exported PDF fidelity | NOT RUN | No physical devices; PDF snapshot action alone is not export validation |

### Finance

| Surface / workflow | Status | Observed result |
| Settings | SMOKE | Settings surface available; no multi-currency or tax matrix tested |
| Wallet creation | PASS | Tenant wallet type and credential created; synthetic initial balance USD 100 |
| Wallet list / summary | PASS | Current balance 125; hold-related explanations available on hover |
| Wallet transaction history | PASS | Four records and running balances 100 -> 127.50 -> 140 -> 125; [evidence](screenshots/wallet-transactions.png) |
| Wallet workflows tab | SMOKE | Deposit / withdrawal / adjustment controls render; no settlement submitted |
| Statements viewer | PASS | One wallet; credits 140, debits 15, balance 125, four lines |
| Statement generation | FAIL B04 | Submission fails on repeated attempts |
| Deposits, withdrawals, transfers, batch, operations | FAIL B01 | Repeated load errors; empty/zero states are not trustworthy |
| Audit, reconciliation, outbox, approvals, policy, refunds/disputes | FAIL B01 | Repeated load errors |
| Financial reports | FAIL B01 | Persistent report-unavailable message |
| Approval, settlement, reconciliation, retry and dispute processing | BLOCKED | Cannot validate through failing operational screens |
| Mobile statements | SMOKE | Cards/actions/tabs render; full mobile workflow suite not executed |

### Completed Scenario Details

- PASS: Create isolated tenant; repair feature dependencies; enable Inventario/POS reporting and inventory operational features.
- PASS: Basic Inventario setup creates default stock area/location.
- PASS: Create product with inline category and new brand; navigates to product detail.
- PASS: Add 20 units from product detail; persisted stock balance shows 20 on hand / 0 reserved / 20 available.
- PASS: Create wallet type, user/credential, wallet (100.00 initial synthetic balance), and POS register scoped to the QA tenant.
- PASS: Create QA Dining layout with table QA-T1; enable and require table selection; save and use it when holding a sale. Resume retained the selected table at payment.
- PASS: Cash checkout of base + Large variant: USD 27.50, cash received 30.00, change 2.50. Second base sale: 12.50. Variant return: 15.00, completed.
- PASS: Cross-module reconciliation after return: inventory 30 units (base 22 + variant 8); wallet 125.00 = 100 initial + 27.50 + 12.50 - 15.00. Statements show credits 140 and debits 15. [Stock](screenshots/inventory-after-return.png), [ledger summary](screenshots/finance-after-sales-return.png).
- PASS: POS report reconciles two receipts, gross 40.00, one refund 15.00, net 25.00. Share dialog opens and Copy link changes to Copied. [Evidence](screenshots/pos-share-link.png).
- PASS: Compact receipt template and merchant name persist to sale receipt preview, with correct items, totals, tendered amount and change. [Preview](screenshots/pos-receipt-preview.png). Physical printing and PDF output fidelity are not yet verified.

## Confirmed Bugs

### B01 - Finance operational lists and reports fail to load (High)

Reproduced on two full navigations to `/finance/deposit-requests` in the QA tenant, after successfully creating a wallet. The page emits five identical "Could not load deposit requests" toasts, but displays Pending/Approved/Rejected = 0, Total Amount = 0.00, and "No deposit requests found". Expected: load successfully, or show a single persistent error without presenting failed data as factual zeros. [Screenshot](screenshots/finance-deposits-failure.png). Repro video: N/A (visible on load). Root cause not established by black-box testing.

The same load-failure pattern was observed on Withdrawals, Transfers, Batch, Operations, Audit, Reconciliation, Outbox, Approvals, Policy, and Refunds, including repeat navigation checks. Financial Reports separately shows a persistent "Report unavailable" / "Financial data could not be loaded" error. Wallets and Statements load successfully and reflect the completed synthetic sales and return; this is not a globally unavailable application. Do not infer these failed operational lists are genuinely empty. Approval execution, settlement, reconciliation, refund/dispute processing and batch completion are blocked pending investigation.

Reproduction: select `QA Modules 20261022` as administrator, open each route below, observe errors, then reload. Wallet features were enabled. The error does not establish whether the underlying cause is authorization, configuration, API failure, or query failure.

| Route | Evidence |
| --- | --- |
| `/finance/deposit-requests` | [Deposits](screenshots/finance-deposits-failure.png) |
| `/finance/withdrawal-requests` | [Withdrawals](screenshots/finance-withdrawal-requests.png) |
| `/finance/transfers` | [Transfers](screenshots/finance-transfers.png) |
| `/finance/batch` | [Batch](screenshots/finance-batch.png) |
| `/finance/wallet-operations` | [Operations](screenshots/finance-wallet-operations.png) |
| `/finance/wallet-audit` | [Audit](screenshots/finance-wallet-audit.png) |
| `/finance/reconciliation` | [Reconciliation](screenshots/finance-reconciliation.png) |
| `/finance/outbox-webhooks` | [Outbox](screenshots/finance-outbox-webhooks.png) |
| `/finance/wallet-approvals` | [Approvals](screenshots/finance-wallet-approvals.png) |
| `/finance/policy-decisions` | [Policy](screenshots/finance-policy-decisions.png) |
| `/finance/refunds-disputes` | [Refunds/disputes](screenshots/finance-refunds-disputes.png) |
| `/finance/reports` | [Reports](screenshots/finance-reports.png) |

### B02 - Saving an existing reorder rule terminates the application circuit (High)

Route: `/inventario/planning/rules/f004dce3-edb9-4deb-b31e-c66af2ede981`. Reproduced twice, including a minimal edit of reorder quantity from 1 to 2. The page displays "An error has occurred. This application may no longer respond until reloaded"; browser console reports an unhandled exception and terminated circuit. Reload shows the original values, so edits were lost.

1. Open the QA rule and select Edit details ([before](screenshots/reorder-edit-start.png)).
2. Change Reorder Quantity from 1 to 2 using normal keyboard entry ([edited](screenshots/reorder-edited.png)).
3. Save changes; the application disconnects ([result](screenshots/reorder-crash-reproduced.png)).

Expected: persist the rule or display field validation without terminating the session. Video capture was attempted, but encoding failed because ffmpeg is unavailable; the reproducible before/edit/after screenshots are retained. The recording harness recreated its browser context and required tenant reselection; that setup step is not an application defect.

### B03 - Cashier base-product availability includes variant stock (Medium)

After the tested sales and return, Inventory shows base stock 22 (20 untracked + 2 lot-tracked) and Large variant stock 8, total 30. The Cashier displays **30 available** on the base-product tile and **8 available** on Large, counting Large stock in both displays. Reproduced across reloads and also observed before return (base tile 30, variant 7, actual base 23). Expected: a purchasable base SKU should show its own available quantity (22), not aggregate product-family inventory. This may encourage unfulfillable orders; overselling itself is not asserted. [Cashier](screenshots/pos-availability-after-return.png), [stock rows](screenshots/inventory-after-return.png), [reservation form independently shows base availability 22](screenshots/reservation-form.png). Video: N/A, visible-on-load discrepancy.

### B04 - Generate Statement fails although statement data loads (High)

Reproduced twice, including after a full reload of `/finance/statements`, with one funded QA wallet and four ledger lines. Select Generate Statement; the toast says "Statement generation could not be submitted." No generated statement is presented. Expected: submit generation successfully or explain an actionable prerequisite. [Before](screenshots/statement-before.png), [failure](screenshots/statement-generate-failure.png). Root cause not established; this may share an underlying cause with B01, but it is a distinct failed user action. Video unavailable due to the recording encoder limitation noted above.

## Workflow Improvements

- W01: A newly created tenant immediately displays "Enabled features have missing required dependencies". The default feature configuration should be internally consistent, or tenant creation should offer coherent presets. "Fix all" successfully enabled four prerequisites. [Evidence](screenshots/new-tenant-dependencies.png). This is an onboarding defect/workflow issue, not a failed stock operation.
- W02: New-tenant wallet creation depends on a credential and wallet type, but its pickers have no create shortcut. Creating a type requires tenant Reference Data; that route changes the header to All Tenants, and selecting the tenant while on that route reverts to All Tenants. Leaving the admin route and selecting the tenant again restores operational context. Provide a guided Finance/POS setup path and make the edited tenant scope explicit. No cross-tenant write is asserted.
- W03: A stock adjustment of -100 against 20 available correctly failed, but only with "Could not post stock movement." Explain the rejected quantity and available stock in the dialog. [Evidence](screenshots/negative-stock-rejection.png).
- W04: A bottom-right POS success/resume toast covered the Pay area at the tested desktop viewport. Make notifications non-obstructive to checkout, or position them away from the primary action. [Evidence](screenshots/pos-held-resume.png).
- W05: Reservation totals counted loaded historical records while the Active filter displayed no results. Distinguish total loaded from matching active records. [Evidence](screenshots/inventario-reservations.png).
- W06: Inventario product sales showed one Large unit / USD 15 even after its return, because the view is explicitly gross before refunds. This is not an arithmetic defect, but net quantity, returned quantity and net revenue would make this view useful for purchasing/planning. [Evidence](screenshots/inventario-transactions.png).
- W07: Reorder-rule metadata uses literal question marks between product/variant/warehouse labels. Replace these with deliberate separators. [Evidence](screenshots/reorder-edit-start.png).
- W08: Mobile navigation is inconsistent: Inventario uses a drawer while POS/Finance retain a sidebar or icon rail. On a 390px screen the expanded POS sidebar leaves very little room for product search. Default to a drawer on narrow screens. Cart/payment remain reachable after scrolling and the payment dialog itself fits. [Expanded POS](screenshots/mobile-pos-cashier.png), [collapsed POS](screenshots/mobile-pos-collapsed.png), [payment](screenshots/mobile-payment.png), [Inventario](screenshots/mobile-inventory-products.png), [Finance](screenshots/mobile-finance-statements.png).

## Missing Capabilities

These are **observed workflow gaps or capability requests**, not claims that the corresponding backend feature is absent. Distinguish missing functionality from implemented-but-broken functionality (B01/B02/B04) and from untested functionality.

| Gap | Evidence / current boundary | Recommended capability |
| --- | --- | --- |
| Guided Finance/POS onboarding | Credential and wallet type must be prepared on separate Identity/Reference Data pages before wallet/register setup | Tenant-scoped setup checklist and inline creation with return to the original form |
| Net product sales in Inventario | Sales summary is gross before returns | Returned quantity, net units and net sales by product/date, reconcilable to POS |
| Source-to-destination stock transfer in the movement dialog | Transfer option is disabled and says it requires source/destination; no transfer was completed | Dedicated paired transfer action with both positions and linked ledger entries; confirm existing alternate transfer surfaces before adding another |
| Actionable operational error state | Generic toasts, repeated alerts and failed requests presented as zeros | Persistent error state, retry, preserved filters and a support correlation identifier; never substitute failure with empty-data claims |

Capabilities already present and therefore **not** labelled missing: brand list/merge dialog, inline category and brand creation, product stock pages, movement descriptions, table layouts and table selection, three receipt template options, receipt section controls, POS/Inventory charts, share-link copy dialog, and wallet balance tooltips. Some of these still need the deeper execution coverage listed next.

## Remaining Coverage / Retest Plan

The following did **not** receive an end-to-end pass in this audit:

- Finance: fix B01 and B04 first, then exercise deposit/withdrawal approval and settlement, transfers, batch completion, reversals, holds/releases, freeze/close approval, reconciliation, outbox retry, policy decisions, refund/dispute processing and statement download. No real gateways were invoked.
- Inventario: fix B02, then test saved thresholds, low-stock alerts and replenishment suggestions. Also complete PO remainder/cancellation, supplier-detail history, actual brand merge and product reassignment, category hierarchy changes, variant-combination pricing, lot expiry/traceability and warehouse-to-warehouse transfer. Basic receipt, single variant and reservation/release are covered; these additional branches are not.
- POS: customer-wallet checkout and new-customer creation, insufficient balance, discount/tax combinations, duplicate/double-submit payment prevention, full/partial/over-limit return boundaries, multiple registers/tables/layouts, logo upload, all receipt templates and designer permutations. Return restoration was tested for one variant only.
- UI: complete light-theme coverage, keyboard-only and screen-reader accessibility, every modal/table at mobile widths, long labels, large datasets and pagination/filter combinations. Mobile checks here are selected spot checks, not full mobile certification.
- Security/reliability: lower-privilege accounts, cross-tenant negative authorization, concurrent cashiers, stale stock races, network interruption/recovery, session expiry, sustained load, real scanners and printers remain untested.
- Export: receipt preview was inspected visually, but physical paper size and generated PDF contents were not validated. A PDF snapshot click without an inspected file is not counted as success.

Recommended order: repair Finance read failures and statement generation; repair reorder save; correct base-SKU availability; repeat the complete transaction reconciliation scenario; then execute the remaining authorization/concurrency and export matrix before release sign-off.

## Test Data and Limitations

- Dedicated tenant: `QA Modules 20261022`, ID `72099247-9d0d-4290-8364-29feb88a422b`.
- Product: `QA Coffee`, SKU `QA-20261022-COFFEE`, ID `004a5950-5b07-4278-8e57-dfbf58f1fb22`, base price USD 12.50.
- Category `QA Beverages`; brand `QA ROASTERS`; stock area `QA Main Stock`; opening stock 20.
- Added Large variant at USD 15.00 (+2.50), with 8 units. Created supplier `QA-SUP-20261022`, PO `QA-PO-20261022` for 10 at USD 5.00, and receipt `QA-RCV-20261022-A` for 4 units into `QA-LOT-20261022`. PO became PartiallyReceived; product stock reconciled to 32. [Evidence](screenshots/inventory-stock-received.png).
- Created tenant wallet type `QACASH22`, user `QA Cashier`, credential `qa.audit.20261022`, and wallet account `102703953036` with 100.00 synthetic initial balance. No real funds or external providers involved.
- Administrator account used. No lower-privilege authorization claims are implied by these tests.
- Wallet ID: `f6e65515-2e40-4624-9fad-35ae0498c089`; final tested balance 125.00.
- Register: `QA-REG-20261022`; table layout `QA Dining`, table `QA-T1`.
- Sales: `POS-20261022151909-67bb84f9c2b` (27.50) and `POS-20261022152037-18bc8142231` (12.50). Return: `RET-20261022152124-7ed0a5919e5` (15.00).
- Reorder rule `f004dce3-edb9-4deb-b31e-c66af2ede981` remains at its original values (minimum/reorder point 0, quantity 1) because edits failed. Do not interpret the absent low-stock signal as a separate bug.
- The manual reservation was released; both held sales were completed; the extra mobile-test cart item was removed without payment. QA records are retained for reproduction, not deleted.
- Evidence: PNG screenshots linked above. Video recording was attempted, but ffmpeg was unavailable; no usable reproduction video is claimed. Earlier CI/unit-test results are not counted as evidence for this live audit.
- No product fixes, commits, PRs or deployment were performed as part of this QA request.
