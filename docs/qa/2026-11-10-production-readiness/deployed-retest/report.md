# Deployed POS Recovery Retest

Date: 2026-11-10. Live financial tests ran approximately 15:31-15:39 UTC.

## Release

- Fix PR: [#620](https://github.com/kaizendevsio/XFramework/pull/620), merged after all CI checks passed; administrator override covered reviewer approval only.
- Source commit: `fb73e8dfd1cb7f0757ab6727a12ca606a5a677bd`.
- Merged/deployed commit: `cd60d3d08851696160e4890fd83c4e74f8aef432`.
- [Full-stack deployment 42841918147](https://github.com/kaizendevsio/XFramework/actions/runs/42841918147): success, including migration, authenticated Bolt/core checks, diagnostic leak scan, Portal runtime and ingress checks, and release activation.
- Bolt CI [42839028585](https://github.com/kaizendevsio/XFramework/actions/runs/42839028585), POS Scanner [42839028538](https://github.com/kaizendevsio/XFramework/actions/runs/42839028538), Identity [42839028735](https://github.com/kaizendevsio/XFramework/actions/runs/42839028735), CodeQL and secret checks passed. No failed-check exemption was used.
- POS, Inventario and Portal containers were healthy. Their running image IDs matched the immutable merged-commit tags, respectively: `sha256:48557e3e95023cf354eee02747e1d457b61af4aada5e74af8175dbc37d28b49c`, `sha256:f4b029563590b0c4ecb55299fbc02cfb337f7b8a5fe002f0210cfd902fe3566e`, `sha256:4e75af43e511eb995de8ea1a2d799e269e2a4c80ee67f42e0a4ec16056246d6e`.

## Scope and Method

Target: `http://xeon-dev:5000`, tenant Jay Test 01 (`69fa3896-0afe-42fa-a11f-5e265207d9d6`), existing synthetic QA register and merchant wallet. All financial actions used the Portal UI, with fresh-page reads to verify persisted recovery states. This was a Super Admin workflow test, not restricted-role acceptance.

Fault injection was limited to temporarily disabling the tenant's wallet withdrawals, then wallet deposits, separately through tenant settings. Both were restored. No shared service was stopped, no database state was edited directly, and no real external payment provider was involved. Audit records were retained.

## Results

| Workflow | Result | Persisted evidence |
| --- | --- | --- |
| Checkout | PASS | One base shirt at USD 12.50 plus one Large at USD 15.00; cash received USD 30.00, change USD 2.50. Sale `c72c55c9-e32a-4540-a662-aef74511291d` completed; stock became 5 base / 3 Large; wallet credit exactly USD 27.50. |
| Partial refund | PASS | Return `d2e5795d-509d-430e-b86c-09819efae8b3` completed for one Large / USD 15.00. Remaining return quantity for Large became zero; wallet became USD 12.50. |
| Refund failure and retry | PASS | With withdrawals disabled, base return `e2540fec-fc83-4e4c-bc1b-c4cb783300ea` persisted as RefundFailed after restocking. Fresh page showed the feature-disabled cause. After restoring withdrawals, Retry Return completed and a fresh page confirmed Completed. Refund reference stayed `POS-RETURN-e2540fecfc834e4cbc1bc4cb783300ea`. Exactly one USD 12.50 debit and one base return movement exist; retry did not restock twice. |
| Failed-payment cancellation | PASS | With deposits disabled, one-base checkout `9d44893b-6b95-4302-b58c-f2027719bbe6` persisted PaymentFailed, with a reservation followed by release and no wallet credit. Cancel Sale completed; hard reload confirmed Cancelled. Deposits were restored afterward. |
| Navigation and persistence | PASS | New sale and return links opened; fresh page loads retained RefundFailed, Completed and Cancelled at their respective stages. |
| Reconciliation | PASS | Final stock: original T-shirt 10, QA base 6, QA Large 4; all reserved quantities zero. Merchant wallet posted/available/total/transferable balances and holds all zero. |

The warehouse has one General stock location, three balances and 23 movements (13 before this run). The ten new movements are two reservations, two fulfillment releases, two shipments, two returns, and one reservation/release pair for the failed checkout. No additional return movement was created by retry.

Wallet `a3074d44-61e7-44f9-ba46-7c2580e881f4` has six total historical transactions, exactly three from this run: +27.50, -15.00, -12.50. No entry references the cancelled checkout. Final tenant configuration is unchanged: Inventario 9/14 features, POS 4/5, Wallets 8/8. The cashier cart is empty.

## Remaining Limits and Observation

These results close the development candidate-deployment gate, not the remaining [production shipping gates](../report.md#remaining-shipping-gates). Real process death/lost acknowledgments, production restore/rollback, real restricted roles, operational alerts, capacity, physical equipment and external payment settlement remain unverified in this run. Refund retry was tested against a definite tenant-feature failure, not an ambiguous gateway timeout.

One non-blocking presentation issue was observed: the failed payment's Payment Audit row labels its requested USD 12.50 as "Captured Amount" even though status is Failed and there is no captured timestamp or wallet credit. The ledger and cancellation behaved correctly, but that column should distinguish attempted amount from captured amount in a follow-up. This retest did not change that UI.

## Screenshots

- [Checkout payment](checkout-payment.png), [completed sale](sale-completed.png).
- [Partial refund](partial-refund.png), [intermediate wallet](wallet-partial.png).
- [Persisted refund failure](refund-failed.png), [completed retry after reload](refund-retry-completed.png).
- [Failed checkout](payment-failed.png), [persisted cancellation](sale-cancelled.png).
- [Restored wallet](wallet-restored.png), [wallet ledger](wallet-ledger.png).
- [Restored warehouse balances](warehouse-balances-restored.png), [movement ledger](warehouse-movements.png).

Screenshots show the current viewport; the balance and ledger counts above were also read from the complete rendered page content. No authentication state or credentials are included in this evidence.
