# Bug Fix Verification - Inventario, POS, Finance

Date: 2026-10-23 Singapore (browser timestamps below are UTC, 2026-10-22).

Status: Targeted bug-fix verification completed. B01-B04 passed the live checks below; provider settlement and separate-checker resolution remain blocked by prerequisites. This is not full release certification. This supplements, and does not overwrite, the [original audit](report.md).

## Scope and Environment

- Target: http://xeon-dev:5000/
- Isolated tenant: `QA Modules 20261022`, `72099247-9d0d-4290-8364-29feb88a422b`.
- Source: uncommitted changes on `codex/pos-inventory-workflows`, based on `3d5a564d`. No PR created or merged for these fixes.
- User authorized an unmerged Xeon Dev verification deployment with existing data preserved.
- No schema changes, volume resets, real payment providers, or business-tenant mutations.
- Live Chromium UI tests use real backend responses. Automated tests are listed separately and do not substitute for browser passes.

## Implemented Fixes

| ID | Root cause and change | Live verification |
| --- | --- | --- |
| B01 | Finance generated entities and actor endpoints rejected delegated tenant administration. Added capability-protected delegated tenant policy, preserving operation-specific capabilities, service-only boundaries and maker-checker rules. Failed list loads now render persistent retry states instead of misleading zero summaries and repeated toasts. | PASS: all eleven affected operational routes reloaded in the QA tenant without former load errors. |
| B01 reports | QA wallet type had no currency; totals correctly refused to mix unspecified currencies. Added a tenant-scoped currency picker to wallet-type editing and an actionable report prerequisite message. | PASS: prerequisite message shown, USD saved through the picker, reports load and reconcile. |
| B02 | Empty optional preferred supplier became null; accessing `.Length` before the save handler threw and terminated the circuit. Made parsing null-safe; corrected literal question-mark metadata separators. | PASS: blank-supplier quantity edit saved and survived reload; threshold edit produced a suggestion. |
| B03 | Base stock query included the product family. Filter to exact variant identity, including null for the base SKU, before summing. | PASS: base 22, Large 8. [Screenshot](screenshots/retest-pos-stock.png). |
| B04 | Statement endpoint rejected delegated tenant access; Portal also discarded the returned statement lines. Retained typed results and display them in a filterable, paginated statement dialog; discard late results after a tenant change. | PASS: generated six ledger rows with ending balance 125. Dialog fits desktop and mobile; overflow stays within the dialog. |

## Completed Live Retests

| Workflow | Result | Evidence / assertions |
| --- | --- | --- |
| Finance operational navigation | PASS, reads | Deposits, withdrawals, transfers, batch, operations, audit, reconciliation, outbox, approvals, policy and refunds load without the former authorization errors. Actual ledger/operation rows are visible, rather than false empty results. |
| POS stock availability | PASS | QA Coffee base 22, Large 8, matching inventory; cart remains empty. |
| Reorder-rule edit/reload | PASS | Reorder quantity 1 -> 2 with blank preferred supplier, saved without circuit failure; reload retained 2. [Evidence](screenshots/retest-reorder-saved.png). |
| Planning threshold/suggestion | PASS, limited | Saved minimum and reorder point 30; planning shows one low-stock position and suggested quantity 2. The product-level rule aggregates 30 family units; this test does not establish exact-variant planning semantics. [Evidence](screenshots/retest-planning-suggestion.png). |
| Overview low-stock drilldown | PASS | Low-stock count 1; clicking it navigated to Planning. |
| Generate statement | PASS | Six entries with running balances 100 -> 127.50 -> 140 -> 125 -> 126 -> 125. Heading and close control remain visible at 1258x566 and 390x844. Mobile dialog bounds x=16..374, y=56.75..787.25; its 1050px-wide grid scrolls within a 308px container. Close worked. [Desktop](screenshots/retest-generated-statement.png), [mobile](screenshots/retest-statement-mobile.png). |
| Financial report setup and totals | PASS | Actionable missing-currency message; edited QACASH22 to USD using the new picker; reports show USD balance/available 125, zero holds, credits 141, debits 16, six entries. [Picker](screenshots/retest-wallet-currency.png), [balances](screenshots/retest-financial-balances.png), [activity](screenshots/retest-financial-activity.png). |
| Batch increment and decrement | PASS | Synthetic amount 1.00 credited, then debited through UI. Both operations Completed; wallet 125 -> 126 -> 125. [Completed batches](screenshots/retest-batch-completed.png). |
| Reconciliation | PASS, submission and balance check | Run Drift Checks confirmed and returned `Reconciliation submitted.` Balance 125, available 125, transferable 125, drift 0, Reconciled. No drift repair was needed/tested. [Evidence](screenshots/retest-reconciliation-action.png). |
| Outbox replay | PASS | Replayed the QA batch-decrement completion event. Observed Published -> Pending -> Published; six published, zero pending/failed/dead-letter after reload. [Evidence](screenshots/retest-outbox-republished.png). |
| Refund case creation | PASS | Opened a synthetic 1.00 refund case against the QA 12.50 sale credit at 16:04:34 UTC. No settlement executed. |
| Refund case resolution | BLOCKED, safeguard verified | Same requester attempted rejection; server returned `Requester cannot resolve their own wallet case`. Case remains Open. Requires a separately authenticated authorized checker; no bypass added. UI still presents a generic action-failure toast, an improvement opportunity. |
| Deposit settlement | BLOCKED, setup | Create dialog opens and QA wallet is selectable, but no QA payment gateway exists. No request submitted to an unrelated provider. [Evidence](screenshots/retest-deposit-gateway-prerequisite.png). |
| Withdrawal settlement | BLOCKED, setup | Create dialog loads; gateway picker has no payout provider. No external cash-out attempted. [Evidence](screenshots/retest-withdrawal-prerequisite.png). |

## Automated Regression Results

| Suite / filter | Result |
| --- | --- |
| POS `PosCatalogServiceTests` | 13 passed |
| Wallets `FinancialReportTests` and `GeneratedEntityAuthorizationCompletenessTests` | 18 passed |
| Wallets `PortalContractTests` and `WrapperContractCoverage` | 18 passed |
| Trusted invocation resolver | 31 passed |
| Portal `ReferenceDataSaveTests`, `ReorderRuleSaveTests`, and Finance tests | 41 passed |
| `git diff --check` | Passed (line-ending warnings only) |

Tests include null/empty preferred supplier, exact base/variant stock, delegated statement capability denial, generated authorization metadata, wallet-currency patch persistence and cross-tenant currency rejection. An initial Wallets test run lacked the remote Docker environment and failed fixture setup; the corrected PostgreSQL/Bolt-backed rerun passed. Existing MessagePack NU1902 and compiler warnings remain outside this fix scope.

## Data Retained

- Existing stock and sale/return records are preserved. Stock remains base 22 and Large 8.
- QA wallet `f6e65515-2e40-4624-9fad-35ae0498c089`, account `102703953036`, remains 125.00 after the offsetting batch pair.
- Added batch increment at 16:09:09 UTC and decrement at 16:09:43 UTC, each 1.00 with no fee.
- One 1.00 refund case remains Open awaiting a different checker; no balance impact.
- Wallet type QACASH22 now uses the tenant's USD currency. No balances were converted.
- QA reorder rule now has minimum/reorder point 30 and reorder quantity 2; preferred supplier remains blank.
- No deposit/withdrawal request or real payment-provider transaction was submitted.

## Remaining Boundaries

These checks are not a complete module release certification. The original audit's untested capability matrix remains applicable. In particular, second-user approval/settlement, provider callbacks, two-wallet transfers, reversal/hold/freeze/close permutations, lower-privilege browser coverage, concurrency, physical printing and statement download are not claimed as passing. Statement generation now targets an on-screen ledger result, not a new PDF/export capability.

## Verification Deployment

- Remote target: OpenSSH and Docker context `xeon-dev`; Compose project `xframework`.
- Rebuilt/recreated only Wallets, POS and Portal using `xframework/<service>:qa-fixes-20261022`.
- Image IDs: Wallets `1d36f837ac32`, POS `7bec2f655343`, Portal `6854b166c56b`.
- Final Portal recreation: 2026-10-22 16:21 UTC. All three services confirmed healthy afterward.
- Compose used the existing release's `candidate.env`, `docker-compose.yml`, `images.override.json` and `/tmp/qa-fixes-20261022.override.json`, with `up -d --no-build --no-deps` for the affected services.
- No migration, volume reset or data deletion. Changes remain uncommitted and unmerged.
- Rollback: recreate those three services using the same release configuration without the QA image override. Preserve volumes and the retained QA records.
