# Verification Bug Fixes and Retest - 2026-10-29

Scope: B01, B02 and B03 from [the verification report](report.md). This is targeted regression verification, not a full production sign-off.

## Changes

- B01: POS requests allocatable stock balances. Inventario excludes expired, quarantined, consumed and rejected lots from this opt-in read, including date-expired lots still marked Available. Default inventory reads retain physical balances. Positive untracked stock remains eligible. The reservation service remains authoritative at checkout.
- B02: The main Portal layout renders service confirmations after the container/overlay portal hosts so confirmation/cancel controls stack above their parent dialog.
- B03: Receipt settings editor is keyed to its draft. Save, reload and discard replace the editor state, including dropdowns and preview.

## Automated Regression Results

All 55 targeted cases passed:

| Suite | Passed | Coverage |
| --- | ---: | --- |
| Inventario StockReservationTests | 13 | PostgreSQL and Bolt wrapper; lot eligibility, physical balance visibility, eligible and untracked stock, existing reservation/release/fulfillment/idempotency tests |
| POS catalog service/search | 28 | Catalog search, stock scope, base/variant separation, pricing and forwarding of eligibility filtering |
| Portal targeted browser/contract tests | 6 | Receipt settings save/discard at 1920, 768 and 390 px; brand merge cancel/confirm at desktop/tablet; production provider ordering |
| Portal production-layout picker regressions | 8 | Select, Escape, advanced search and outside-click closure, with and without delayed registration |

TRX files are under each test project's ignored `TestResults` directory: `verification-stock.trx`, `verification-catalog.trx`, `verification-ui.trx`, `verification-dialog-host.trx`. Browser tests use real Blazor components with fixture service responses; inventory integration tests use disposable PostgreSQL and real Bolt wrappers. Live deployment retests are recorded separately below.

## Deployment and Live Retest

Completed on Xeon Dev (`http://xeon-dev:5000`) using Docker context `xeon-dev` and SSH `xeon@xeon-dev`. Source base: `43241b48e0620eb0805a56083735a53f27285d19`, branch `codex/pos-inventory-verification-fixes`. This is a branch test deployment; changes have not been committed, pushed or merged.

Only Inventario, POS and Portal were rebuilt/recreated. Existing environment/secrets and data volumes were preserved; no migrations or database resets were run. All three containers report healthy. Their logs since restart contained no matches for `fail:`, `error` or `exception` at the end of verification.

| Service | Image ID | Started UTC |
| --- | --- | --- |
| Inventario | `bf42576c44693c711ed55aec747dbe1584bcb4fe16b0ba06b8c0eae6cebcc3a4` | 2026-10-28 17:34:29 |
| POS | `f88ce01914c77606dd7bbba600a10708e68d89fd85db4c4287d769703d85f33c` | 2026-10-28 17:34:36 |
| Portal | `e070399a7ecfe355e40ff50e2d707df50147fa9f08e8cbd740670729c6143a27` | 2026-10-28 17:39:35 |

These UTC times are October 29 in the local Asia/Singapore timezone. Images use `xframework-<service>:qa-fixes-20261029`. Deployment used the existing release `39825885286-1` compose, env and image override plus `/home/xeon/qa-fixes-20261029.override.json`, with `up -d --no-deps --no-build --pull never --wait --wait-timeout 180 inventario pos`, then the same command for `portal`. The original release files were not overwritten. Rollback uses those original files without the extra override and recreates only the same services.

Tracked source diff was retained at `/home/xeon/qa-fixes-20261029-source.patch` (SHA256 `F7577826B8AFA7CEC34E3393F577380B1891314D318D8F1B4BC0C02063E76449`). The patch excludes untracked report files and the new source-contract test, neither of which is needed by the deployed application.

### Live Browser Results

Real services, no mocked responses, synthetic tenant `QA Modules 20261022` (`72099247-9d0d-4290-8364-29feb88a422b`).

| Bug / workflow | Result | Evidence |
| --- | --- | --- |
| B01 expired-lot catalog | PASS | Before deployment, QA Integrity 20261029 base product advertised 1 expired unit. After deployment, that base product is absent from sellable catalog; Large Dark QA remains available at USD 17.25 with 3 units. Physical inventory still shows the expired lot. |
| Eligible checkout and return | PASS | Cash sale `POS-20261028173802-27d01af17a3` completed at USD 17.25 with required table QA-T1, exact tender, zero change; variant stock went 3 to 2. Full return `RET-20261028173919-6b5fcfbaee7` completed for USD 17.25 and restored variant stock to 3. Reserved stock is 0. |
| B02 merge confirmation | PASS | Confirmation appears above parent. Normal Cancel returns to the unchanged merge form. Reopening and confirming merges QA MERGE SOURCE 20261029 into QA ROASTERS, affecting exactly 1 product. Source disappears; product summary shows target brand. Base price 10, stock 4 total (3 variant + 1 expired base), and reserved 0 remain unchanged. |
| B03 receipt Discard | PASS | Saved Compact / 80 mm changed to Bold / 58 mm plus an unsaved merchant name. Discard immediately restores Compact / 80 mm / QA Coffee Shop and its preview. A fresh page load confirms unchanged saved settings; preview markup specifies compact and 80mm. No settings save performed. |

Screenshots in `screenshots/`: `retest-before-catalog.png`, `retest-after-catalog.png`, `retest-merge-confirmation.png`, `retest-merge-success.png`, `retest-merged-product.png`, `retest-receipt-draft.png`, `retest-receipt-discard.png`, `retest-stock-restored.png`. Confirmation and discarded receipt screenshots were visually inspected.

Synthetic sale/return and merge audit history are retained. Stock was restored by a real return, not by direct database edits. The test product now deliberately belongs to QA ROASTERS; no business records were used.

## Remaining Boundaries

All three defects from the original verification are closed by this targeted retest. This does not close its broader coverage gaps: warehouse-transfer UI, restricted-user/cross-tenant isolation, backend crash during settlement, real payment-provider settlement, exported PDF artifacts and physical hardware. Those require separate workflows and evidence, not a claim based on these 55 tests.

Existing package/build warnings remain, including the MessagePack NU1902 advisory. They were not introduced or addressed by these scoped fixes.
