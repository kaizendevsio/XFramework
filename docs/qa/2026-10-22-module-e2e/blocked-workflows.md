# Blocked Workflow Follow-Up

Date: 2026-10-23 Singapore.

This follows [the audit](report.md) and [the first retest](retest.md). Historical results in those files describe their original deployment, not the final release status below.

## What Was Blocking These Tests?

| Item | Actual cause | Resolution / boundary |
| --- | --- | --- |
| Empty deposit gateway picker | The shared PaymentGateway entity had no generated remote query wrapper. Portal swallowed that exception and displayed an empty picker. The QA tenant also needed a gateway record. | Added an explicit, tenant-scoped REST/Bolt lookup and safe display DTO; Portal uses its generated wrapper. Added an isolated, zero-fee QA manual gateway fixture. Lookup failures now remain failures instead of appearing as an empty list. |
| Withdrawal gateway | The first retest incorrectly treated the missing gateway as mandatory. The backend supports manual withdrawals with a null gateway. | The picker now explicitly labels the optional gateway and Manual settlement choice. This records ledger settlement; it does not send an external payout. |
| Refund / approval decision | A maker must not approve or resolve their own request. | Created a separate, expiring QA checker role/account. The safeguard remains enforced; Portal explains that a different authorized user must decide. |
| Checker browser session | Portal login currently selects its bootstrap administrator role, rather than offering ordinary tenant-role sign-in. | Use an independently authenticated, least-privilege checker through the API for this verification. This is mixed UI/API coverage, not a two-browser-user pass. Tenant-role Portal sign-in remains a separate capability gap. |
| Real provider settlement | A gateway record is only configuration identifying a payment route. It is not a working bank/card adapter. | Real settlement needs the chosen provider's adapter, sandbox credentials, account provisioning, and authenticated callback integration. No real funds or sample-provider success stub are used here. |

## Additional Fixes

- Gateway lookup supports paging, filters inactive/deleted/other-tenant gateways before paging, and excludes provider endpoint details.
- Gateway reads require Finance Reporting View, consistent with other Finance page reads. Mutations retain their own capabilities.
- Gateway results are discarded if the tenant changes during loading.
- Wallet types accept only active same-tenant or global currencies. An established denomination cannot change or be cleared after wallet, ledger, or deposit usage. Assigning the initial missing currency remains supported as a data-repair action; no conversion is performed.
- `.local/` is excluded from Docker build contexts as well as Git.

## Automated Checks

- 33 Portal tests passed: gateway wrapper/error handling, reference-data and reorder regression tests.
- 35 wallet currency integration tests passed.
- 10 gateway tests passed, covering HTTP/Bolt authorization, tenant scope, enabled/deleted filtering, paging, and generated contract metadata.
- 3 Portal case-form tests passed for missing, malformed, and empty original transaction IDs.
- 13 new withdrawal settlement/recovery regressions passed, including exact balance with fees, unrelated holds, concurrent retries, cached rejection recovery, and changed-request conflicts. The broader run passed 116 tests with one existing case-reference fixture failure. After correcting that fixture, all 23 case/reference and workflow-wrapper tests passed (zero failures/skips), including the previously failing test.
- Read-only independent review of the ordered policy checks, rejection recovery, case references, tenant scope and refund locking found no blocking issues.
- The earlier [121 targeted regression tests](retest.md#automated-regression-results) remain documented separately; the groups above overlap and should not be summed into a unique test count.

## Verification Fixtures

- Tenant: QA Modules 20261022 (`72099247-9d0d-4290-8364-29feb88a422b`).
- Manual gateway: `d3514563-681e-48b4-8ab1-1182a2b8dd80`, provisioned by [the idempotent QA fixture](seed-manual-gateway.sql). No provider endpoint; zero fees. This is not a general gateway-management UI.
- Checker: `qa.finance.checker.api.20261023`; explicit tenant grants are wallets manage, deposit/withdrawal update, wallet policy update/manage, and reporting view. Assignment expires 2026-10-24 23:59 UTC. No tenant-administration grant. The first approval was denied until the separate deposit-feature Update grant was added; endpoint management capability alone does not override feature permissions.
- Separate checker-owned QA wallet: `3cf71dfe-4aa4-4166-bbf7-fcaa65677b54`, account `704518125172`, initially zero.
- The limited checker can operate on this checker-owned wallet, but cannot decide cases on other users' wallets. A probe against the historical QA wallet returned 403 `Actor cannot operate on the requested wallet workflow`; that case remains unchanged. Broader cross-owner administration requires a separately authorized privileged checker and is not claimed here.
- The older QA wallet, existing business tenants, and database volumes are preserved.

## Live Verification

| Workflow | Observed result |
| --- | --- |
| Gateway lookup | PASS: checker REST read and Super Admin Portal picker both return QA Manual Settlement, with zero fees. |
| Manual deposit | PASS, mixed UI/API: created 10.00 through Wallet Detail; independent checker approved and settled through REST. Repeating settlement returned the same operation/transaction and `Deposit already settled`. |
| Case self-decision | PASS safeguard: Portal rejects the requester's decision with `A different authorized user must approve or reject this request.` |
| Independent rejection | PASS, mixed UI/API: checker rejected a separate 1.00 unlinked QA case; Portal refreshed to Rejected. No balance change. |
| Missing original transaction | PASS: Portal retained the dialog and showed `Select the original transaction for this case.` No new case was created. [Evidence](screenshots/case-original-required.png). |
| Exact-balance withdrawal | PASS after B05 fix: retried the original held 10.00 request, without adding funds or deleting its rejection. Settlement completed, balance/holds became zero, and repeating returned the same operation with `Withdrawal already settled`. The immutable rejection is linked from the recovery operation. |
| Positive refund resolution | PASS after B06 fix, mixed UI/API: Portal created a 1.00 refund using only the original withdrawal transaction. Server linked the completed settlement, not the hold. Checker resolved it, balance became 1.00; duplicate resolution returned the same operation without another credit. [Evidence](screenshots/refund-resolved.png). |
| Positive dispute resolution | PASS, mixed UI/API: Portal created a 1.00 dispute against the original deposit transaction. Checker resolved it; duplicate resolution returned the same operation. Final balance, available/transferable balance, debit holds and credit holds are all zero. [Case](screenshots/cases-resolved.png), [balance](screenshots/manual-settlement-zero-balance.png). |

## Additional Bugs Found During Unblocking

- B05: Policy checks did not account for a hold release preceding capture in the same ledger operation. Exact-balance withdrawals could be approved but never settled.
- B06: Case creation accepted missing operation references. The Portal transaction picker did not supply an operation ID, and the backend did not resolve it from the transaction. Approval later failed with `Original wallet operation is required`.

## Retained Records and Release

- Deposit request `bef270fe-f86a-4015-9f0c-b7f45a71c061`: 10.00, Completed.
- Withdrawal request `85781642-f792-44fd-8374-5f068594aac1`: 10.00, Completed; settlement operation `31dbd7e6-580d-4727-b9b4-dc2de8453df0` links the original rejection `e339e091-197f-40f3-b361-876f024c0236`.
- Refund `28f5733e-db13-40c1-b139-4af548f1f7d5` and dispute `cfe9652e-8fc4-4103-adad-c3e76b742e1a`: 1.00 each, Resolved. New checker wallet ends at zero; older QA wallet remains 125.00.
- Verification images: Portal `a394b122d41c` and Wallets `e1178b9a4c4c`, recreated through OpenSSH/Docker context `xeon-dev` with the existing release configuration plus the QA override, `up -d --no-build --no-deps`. Portal at 17:23 UTC and Wallets at 17:24 UTC, 2026-10-22. No migrations, data deletion or volume reset.
- PR: https://github.com/kaizendevsio/XFramework/pull/617. Final CI, merge and deployment status are reported with the delivery message.

## Still Not Claimed

Real provider callbacks/payouts, lower-privilege Portal sign-in, cross-owner checker browser workflows and the original audit's untested capability matrix remain outside this targeted pass. They need provider setup or additional implementation/verification, not disabled security checks. One remaining UI issue is that the cases tab is labelled `Open Cases` while it also includes resolved/rejected rows; status column filtering is available, but the label should be corrected in a follow-up.
