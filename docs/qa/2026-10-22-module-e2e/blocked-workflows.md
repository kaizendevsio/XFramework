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
- The earlier [121 targeted regression tests](retest.md#automated-regression-results) remain documented separately; the groups above overlap and should not be summed into a unique test count.

## Verification Fixtures

- Tenant: QA Modules 20261022 (`72099247-9d0d-4290-8364-29feb88a422b`).
- Manual gateway: `d3514563-681e-48b4-8ab1-1182a2b8dd80`, provisioned by [the idempotent QA fixture](seed-manual-gateway.sql). No provider endpoint; zero fees. This is not a general gateway-management UI.
- Checker: `qa.finance.checker.api.20261023`; explicit tenant grants are wallets manage, wallet policy manage, and reporting view. Assignment expires 2026-10-24 23:59 UTC. No tenant-administration grant.
- Separate checker-owned QA wallet: `3cf71dfe-4aa4-4166-bbf7-fcaa65677b54`, account `704518125172`, initially zero.
- The older QA wallet, existing business tenants, and database volumes are preserved.

## Live Verification

Verification is in progress. Final outcomes and release references will be recorded after the rebuilt services are checked.
