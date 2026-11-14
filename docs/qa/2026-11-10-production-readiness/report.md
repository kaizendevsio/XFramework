# Inventario and POS Production Readiness

Initial pass: 2026-11-10. Follow-up: 2026-11-11 (Asia/Singapore).

## Release Decision

**Current decision (November 11, after the Xeon Dev deployment):** paid-receipt recovery and image rollback passed on Dev. Do not claim production operations sign-off: there is no production environment, backup/restore target, assigned store-role baseline, or on-call destination yet. The owner narrowed this assessment to **recovery, restore and operations**. See the [latest drill](#deployed-recovery-and-operations); capacity and store acceptance are not gates in this scoped decision.

Historical November 10 decision: **NO-GO for unrestricted production rollout.** That pass found and fixed two POS defects and a known vulnerable dependency. The fixes subsequently passed CI, merged in [PR #620](https://github.com/kaizendevsio/XFramework/pull/620), and deployed to Xeon Dev. The [post-deployment retest](deployed-retest/report.md) passed checkout, partial refund, refund retry and cancellation on Jay Test 01, with stock and wallet reconciliation.

This supersedes the remaining-gate assessment in the [October verification](../2026-10-29-pos-inventory-verification/report.md), without replacing its historical transaction evidence. The [November UI retest](../2026-11-07-pos-dialog-sale-links/report.md) remains historical evidence for the dialog and sale-link fixes.

## November 11 Follow-up (Before Deployment)

The payment audit fix is **local, not deployed**. The last deployed retest found a declined USD 12.50 attempt labeled as captured despite no wallet credit. Sale Detail now lists attempted, captured and refunded amounts separately. Failed shows zero captured; pending shows a dash (capture not confirmed); captured and refunded show the original gross capture. No sale, payment or ledger data was changed.

| New check | Result and boundary |
| --- | --- |
| Payment display and receipt regression | **17/17 passed** in the two Portal fixtures. The new Chromium assertion reads the rendered audit cells for pending, failed, captured and refunded payments. Twelve receipt PDF cases also passed. Not a deployed browser retest. |
| POS integration and interruption checkpoint | **131/131 passed.** `CheckoutAsync_InterruptedAfterCapture_ReplayCompletesWithoutChargingAgain` interrupted the first fulfillment call, read persisted `PaymentCaptured` in a fresh no-tracking context and replayed the original checkout. A fresh read and another replay both showed Completed, one captured payment row and exactly one wallet increment call across all attempts. Real PostgreSQL; simulated wallet, inventory and interruption, not a service kill. |
| Inventario integration and repeated stock cycles | **184/184 passed.** Four concurrent workers each reserved, fulfilled and returned one unit ten times on their own product. Every position ended at its opening quantity (5 available, zero reserved), with ten shipments and ten returns. The 40 cycles measured p50 546 ms / p95 598 ms, including fixture lookup over the dev test connection. This is neither sustained load nor cashier checkout latency. |
| Cross-module schema restore | **Passed.** The [repeatable drill](rehearse-dev-schema.sh) streamed a schema-only dump of Xeon Dev to a new, network-isolated PostgreSQL container. Dump plus restore took 2 s; 1,425 catalog entries (relation names/kinds and constraint definitions) matched, covering 191 ordinary tables across 22 schemas. No tenant rows, owners or grants were restored. This is not a production backup or a measured recovery time for data. |
| Development TLS and health | Identity health over HTTPS returned HTTP 200 with certificate verification enabled; Portal, POS, Wallets and Inventario containers reported healthy. Not proof of production ingress, role restrictions or alerts. |

**Recovery finding:** POS persists `PaymentCaptured` before it fulfills stock. A restart at that point differs from a reported fulfillment failure: Sale Detail offers the fulfillment retry only for `InventoryFulfillmentFailed`. Do not charge again, create a replacement sale, or refund just to clear the screen. Preserve the original receipt, checkout payload and key; reconcile the payment and reservation IDs. Engineering can replay the identical checkout with its original key. Before launch, demonstrate that an on-call operator can recover an interrupted sale even if the cashier session is gone, and independently reconcile one capture and one shipment.

## Deployed Recovery and Operations

November 11, 2026, approximately 20:08-20:14 Singapore time. Only POS and Portal on Xeon Dev were redeployed. This is a **local candidate deployed via a Dev Compose override**, not a merge or a production release. The following results supersede the earlier local-only label fix and missing retry action.

| Check | Observed result |
| --- | --- |
| Operator recovers a paid receipt | **PASS, deployed Jay Test 01.** One USD 12.50 base shirt at the QA register. A short exclusive lock on the QA drawer, followed by the new sale-specific fulfillment lock, deliberately left the checkout paid but unfulfilled. No financial rows or statuses were edited manually. The original POS and Portal processes were replaced during rollback and again during redeployment. In a fresh page, Sale Detail still offered **Retry Fulfillment**. Clicking it completed sale `47eae0ba-1bae-405e-b23e-fbf2ee3f1bc1` without the original cashier payload or a second charge. This was a controlled checkpoint followed by real service replacement, not an involuntary outage at every possible instant. |
| Reconciliation and cleanup | **PASS.** One captured payment, one USD 12.50 wallet credit, one shipment. Return `928df0e9-c980-48db-9fa1-f49c6d256dbd` via Portal completed, posted one USD 12.50 debit and one stock return. The drawer returned to zero; QA stock base 6 / Large 4, both zero reserved. The four stock movements were reservation, release, shipment, return. |
| Race and database interruption | **PASS, isolated.** Concurrent operator retry received 409 while checkout owned fulfillment. The disposable PostgreSQL service was stopped before acknowledgment and restarted; a new POS context recovered by receipt ID, with one captured payment and no second wallet call. Real database, simulated downstream dependencies. Full POS integration 132/132; focused Portal 21/21. |
| Backup and rollback | **PASS on Dev, not production.** The [data restore drill](rehearse-dev-ledger-restore.sh) dumped Dev in 1 s (excluded `audit.event` data), restored in 3 s including verification, then restored the pre-change snapshot into another isolated database in 2 s. Row counts and content digests matched for all **46** POS, Wallet and Inventario tables both times; a post-backup marker was absent from the rollback target. No archive or restored tenant data left the host. Separately, the running POS/Portal were rolled back to their prior image IDs, became healthy, and the candidate was redeployed healthy against the unchanged database. This change has no schema migration. |
| Cashier and manager | **PASS for actual logins in disposable IdentityServer tests (2/2); not deployed assignments.** Real password authentication, database-backed role assignments and issued-session validation. Under the tested policy the cashier could create sales but had neither sales update (fulfillment recovery) nor return creation; the manager had both. Neither received register update. Prior Bolt boundary tests also denied an unauthorized refund/register mutation. The shared Dev browser drill used Super Admin; Jay Test 01 has no configured role types. |
| Settlement alerts | **Detector works; delivery unverified.** [Read-only monitor query](settlement-watch.sql) returned 1 stalled sale, 0 stalled returns, 0 open wallet reconciliation exceptions. The sale was `9875300e-203b-455c-b840-8680c660d56a`, created July 3 UTC outside Jay Test 01, with a captured payment and failed fulfillment. Reported to the owner here; not retried or refunded. Manual execution is not scheduled monitoring, paging or human acknowledgment. |
| Running release and payment label | **PASS.** Only POS/Portal changed; database, Wallets and Inventario container IDs stayed the same. Retested the earlier declined Jay receipt: USD 12.50 attempted, USD 0.00 captured. Tested/running DLL SHA-256: POS `d3af719e5e6eb0427991273e0325735ae6ed9da04090aec097aec28a74e35fe6`, Portal POS feature `1e6d1c26d80964e425576b9274490ece01bcccc7228d1cabf9903bb150b19de0`. Dev override `/var/tmp/xfw-paid-recovery-40q7ufn4/override.json` over release `42841918147-1`; omit only this override to restore the previous images. |

Operator procedure: for a paid receipt awaiting stock, open its sale detail and **Retry Fulfillment**, not Retry Payment or a replacement checkout. On an in-progress conflict refresh the receipt. Confirm Completed, one payment reference, one shipment per reservation and no residual reserved quantity. If still unresolved, escalate with receipt and reservation IDs rather than forcing a status change.

## Candidate and Test Boundaries

The bullets below describe the initial readiness pass. The later deployment and financial-mutation checks are recorded separately in the [deployed retest](deployed-retest/report.md).

- Branch: `codex/pos-inventory-production-readiness`, based on `b2b4033d1be79221a8e1f7b579729f566a939511` plus the uncommitted changes in this pass.
- Live target: `http://xeon-dev:5000`; standard full-stack release `42355002692-1`, from successful deployment run `42355002692`. Application containers were healthy when checked.
- Live UI checks exercise the deployed baseline, **not** the new backend fixes. Candidate backend verification uses disposable PostgreSQL databases and the current local binaries.
- Inventario integration tests use the generated wrapper and PostgreSQL. POS persistence tests use PostgreSQL with mocked Inventory/Wallet dependencies. POS Bolt tests use an in-process host and signed test actors, not real restricted Identity accounts.
- Live browser checks are read-only or unsaved drafts. No new shared-tenant sale, refund, stock movement, feature change, service restart, or deployment was performed. Login creates normal authentication/session records.
- No production target, merchant credentials, physical scanner, or thermal printer was available for sign-off. No destructive fault injection was performed on the shared Xeon Dev stack.

## Bugs Found and Fixed

### PR-01: High - POS mutations did not opt into EF tracking

POS configures its database context with `QueryTrackingBehavior.NoTracking`. Sale and return loaders accepted `tracking: true`, but only applied `AsNoTracking()` for false; true therefore inherited the no-tracking default. Cancellation and retry workflows could report success while their mutated status remained unchanged in the database.

Fix: explicitly use `AsTracking()` for mutation loads in `PosSalesService.LoadSaleAsync` and `PosReturnsService.LoadReturnAsync`, retaining no-tracking reads.

Evidence: both new production-default regressions failed before the fix. Cancellation read back as Draft, and a restock retry read back as InventoryPostFailed after reporting completion. After the fix, independent contexts read the expected persisted status. The payment-recovery regression also verifies Completed/Captured through a fresh context with the no-tracking default.

### PR-02: Medium - Concurrent refund allocation surfaced a server error

Twelve independent cashier contexts attempted to refund the same sold unit using different keys. PostgreSQL serializable isolation prevented over-refunding, but a losing allocation could escape as a wrapped serialization exception rather than a useful conflict.

Fix: translate PostgreSQL serialization/deadlock failures in return creation into HTTP/result 409, telling the caller to retry with the original idempotency key. Clear the failed tracked state; do not automatically replay financial side effects.

Evidence: the race reproduced the uncaught error before the fix. Afterward exactly one return succeeded, all losers received 409, persisted returned quantity/refunded amount remained one unit/25.00, and the dependency mocks observed only one stock and one wallet call. This is a bounded allocation-integrity test, not a real-provider settlement or load test.

### PR-03: Moderate Dependency Advisory - MessagePack

NuGet identified [GHSA-qhrr-8q5h-9q3h](https://github.com/advisories/GHSA-qhrr-8q5h-9q3h) in MessagePack 3.1.7. Updated centrally managed MessagePack and MessagePack.Annotations to 3.1.9.

Fresh resolved dependency scans of POS.Api, Inventario.Api, and Portal report no known vulnerable packages. Bolt RPC/authentication regressions pass. This is not a penetration test or a guarantee against undisclosed vulnerabilities.

## Verification Matrix

| Check | Result | Evidence and limitation |
| --- | --- | --- |
| Inventario service integration | PASS: 183 tests | PostgreSQL/generated-wrapper coverage, including stock, reservations, lots, receiving, planning, transfer and authorization cases. Not every corresponding UI workflow. |
| POS service integration | PASS: 130 tests | Final consolidated run includes the new persistence, refund race, authorization and backup/restore tests. Earlier 129-test and 29-test subset runs overlap and are not added to this count. |
| Unit tests | PASS: Inventario 132; POS 155 | Current local source. |
| Bolt transport/authentication | PASS: 37 tests | RPC readiness, inbound request context and transport authentication; synthetic actors. |
| Local Portal module checks | PASS: 356 tests | Component contracts and isolated Chromium layouts/receipts. Initial module-wide run also had 26 live-test configuration failures; those are tracked separately below, not counted as passes. |
| Live cashier, lists and registers | PASS: 26 tests | Final authorized run on the existing E2E Inventory POS tenant; desktop/tablet/mobile layouts, themes, searches, draft quantities, cash/wallet validation, discount/tax, dialogs, list filters and invalid-register-edit/discard. No checkout confirmation or persisted edit. |
| Twenty clients reserving five units | PASS: bounded integrity | No oversell or inconsistent allocation totals. Conflicting clients receive 409; successful reservations are released and availability returns to five. Not a throughput/SLA benchmark. |
| Twelve cashiers refunding one unit | PASS: bounded integrity | One allocation/refund; all other attempts conflict. Wallet/inventory dependencies mocked. |
| No-tracking cancellation/retry persistence | PASS | Fresh-context reads verify saved states, including payment and restock recovery. |
| Lost payment/refund/restock replies | PASS: simulated dependencies | Original accounts/keys reused; restock is not repeated after acknowledgment. Does not kill a real service or verify a real gateway. |
| Unauthorized refund/register update | PASS: Bolt/API boundary | Cashier lacking the capabilities receives 403; register remains unchanged. |
| Cross-tenant register object access | PASS: Bolt/API boundary | Different tenant with a valid view capability receives 404 for the known register. No actual restricted-user browser session tested. |
| Database backup/restore | PASS: disposable rehearsal only | `pg_dump -Fc`, new database, `pg_restore --exit-on-error`; completed sale, line, payment and migration history survive. No pending migrations; rerunning migration does not duplicate payment. Does not validate production backup retention, secrets, all-module data or recovery times. |
| Receipt PDFs | PASS: 12 Chromium cases | Classic/Compact/Bold, 58/80 mm, 2/50 lines, logo, long names and fractional quantities; no horizontal overflow/row overlap, one page and correct PDF width. Rendered Classic 58 mm, Bold 80 mm and the long Compact 58 mm footer visually inspected, including item 50 and totals. Physical roll feeding/cutting remains unverified. |
| Jay Test 01 warehouse/location | PASS: live read/navigation | My stock shows 1 location, 3 balances and 13 movements. Location details show original base stock 10, QA base 6 and Large 4, with zero reserved. Mouse navigation works after scrolling the link into view. |
| Jay Test 01 sale/receipt | PASS: live existing data | Previously fully refunded 27.50 sale opens directly; preview shows 30.00 cash and 2.50 change. No new refund submitted. |
| Dependency scan | PASS: known advisory scan | POS.Api, Inventario.Api and Portal resolved graphs clear after the MessagePack update. |

## Test Harness Repairs

The first Inventario run had 14 failures from stale test assumptions: the positive catalog actor lacked newly enforced capabilities, a setup fixture soft-deleted a unique tenant setup row, and Portal source-contract tests still expected obsolete inline controls/legacy stock content. Updated the fixtures and assertions to current behavior; negative tests still demand 403 and verify the denied entity was not created. The full 183-test rerun passes.

The POS Bolt fixture inherited a private-key path while configuring a test public key. It now explicitly clears that inherited path. The receipt contract now asserts the dedicated receipt document/print module instead of obsolete `window.print` behavior. These are test fixes, not permission relaxation or application bug fixes.

The first broad Portal run passed 356 local tests and failed 26 live cases before executing their workflows because `POS_E2E_*` authentication was not configured. Retests supply an authorized temporary storage-state file and verified existing catalog/register fixtures. One live test additionally assumed an always-visible subtotal; the cashier deliberately omits that row when both discount and tax are zero. It now reads the single cart line total before applying the discount, retaining all subsequent tax/total/validation assertions.

A later live scroll test sampled the catalog count during a register-triggered reload (failure screenshot shows the loading spinner and no browser/HTTP error). It now waits up to 30 seconds for the required sellable tile before checking count and independent scroll geometry. Missing fixture products still fail; the assertion was not removed.

The final live rerun passed all 26 executed tests with zero failures. The explicitly opted-in register-save and financial-mutation tests were not run and are not included in those 26 passes. The broad TRX contains 384 discovered cases: 382 executed and two not executed; the final live TRX contains 27 cases: 26 executed and one not executed (register-save), with financial mutation excluded by the filter. The 356 passing local Portal checks plus 26 passing live checks close the initial 382 executed cases, but do not close the separate production gates below.

## Remaining Shipping Gates

The owner confirmed only Dev exists. Do not require a nonexistent production backup as evidence from Dev. Before putting real store data or money on a new production stack:

1. **Recovery/restore:** promote the tested source through the release pipeline. Configure backup ownership, encryption, retention and an isolated restore destination. Take the first production baseline backup and perform a full restore with grants, audit records, ledger checks and agreed recovery-time/data-loss targets. The Dev rehearsal provides a procedure, not production evidence.
2. **Access:** assign the approved cashier/manager permissions to production identities. Sign in as each, verify normal checkout, manager-only refund/recovery and register-management restrictions. Do not use Super Admin as a substitute.
3. **Alerting:** assign an on-call recipient, schedule the read-only detector via a least-privileged monitoring identity, route nonzero counts **and monitor failures**, and verify a controlled alert is delivered, acknowledged and cleared. Separately reconcile the July Dev exception with its tenant owner.

## Historical Gate Assessment (Before Latest Drill)

| Gate | What still blocks sign-off | Next verification / owner |
| --- | --- | --- |
| Candidate release | Prior backend release verified on Xeon Dev; **new Portal payment fix not on that deployment**. | Release engineering: merge and deploy the candidate through CI; open the known declined Jay Test 01 receipt and verify USD 12.50 attempted, USD 0.00 captured, no capture timestamp. No need to take another payment. See the [prior deployed retest](deployed-retest/report.md). |
| Full-stack interruption recovery | Simulated lost replies and fresh DbContexts do not prove recovery after a real POS, Wallets, or Inventario process dies between commit and acknowledgment. | Engineering/operations: isolated full-stack tenant/environment; interrupt each phase, restart, retry the same keys, reconcile cash/wallet and stock ledgers, verify no duplicate settlement or stranded reservations. Do not interrupt the shared dev stack during other work. |
| Production recovery | Disposable POS data and the full dev schema have been restored separately; no sanitized production backup with cross-module data or compatible rollback has been rehearsed. | Operations: verify backup encryption/retention and restore access; restore to an isolated target, compare sale/payment/stock totals and grants, rehearse image/schema rollback, and record recovery point and elapsed time against agreed RPO/RTO. |
| Production security/operations | Dev health and synthetic authorization checks do not validate production TLS, secret handling, actual role assignment, alerts or incident response. | Operations/security: verify production configuration, real restricted cashier/manager/inventory accounts, tenant isolation, alert delivery for failed settlement/reconciliation and a recovery runbook. |
| Capacity | The 20-reservation race, 12-refund race and 40 stock cycles verify integrity. None measures full-stack checkout latency under sustained peak traffic. | Store owner: supply peak active registers, transaction mix and acceptable latency. Engineering: run that load against an isolated production-like stack, compare latency/errors to targets and reconcile final ledgers. |
| Advanced inventory transfers | Earlier UI testing found Transfer disabled with no usable source/destination workflow. Backend transfer tests passing do not close that UI gap. | Product/engineering: ship and test a supported source/destination UI, or explicitly exclude advanced transfer operations from the initial launch scope. |
| Physical POS hardware | Browser PDF/scan-search checks cannot verify printer drivers, paper feed/cut, USB/Bluetooth scanning or phone-camera pairing in the actual shop. | Store acceptance: test the intended 58/80 mm printers and scanners, including long receipts and repeated scans. Required if those devices are part of launch. |
| External payment settlement | No supported provider merchant/sandbox setup and real settlement/refund evidence in this pass. Internal wallets/cash are not gateway settlement. | Business/payment engineering: choose an eligible provider, configure secrets securely and test success, decline, timeout/webhook replay, refund and reconciliation. Can be excluded only for an explicitly cash/internal-wallet-only launch. |
| Business acceptance | Receipt content, tax/discount rules, returns policy and operational procedures need the business owner's approval for the intended launch. | Business/store owner: approve the exact receipt and configured rules on the release candidate. No regulatory certification is implied by these tests. |

An explicitly limited pilot may exclude external gateways and advanced transfers, but still needs the candidate, recovery, access-control and operational gates. Do not label untested items as passed merely because they require an environment, equipment or business decision.

## Evidence and Reproduction

- November 11: [executed test totals and TRX checksums](follow-up-results.json); raw TRX in `C:/tmp/xfw-ready-20261111`. For the schema-only drill on Windows: `Get-Content -Raw docs/qa/2026-11-10-production-readiness/rehearse-dev-schema.sh | ssh -o BatchMode=yes xeon@xeon-dev "tr -d '\r' | bash -s"`.
- Machine-readable run totals: [test-results.json](test-results.json). Raw TRX/logs are in `C:/tmp/xfw-readiness-20261110` and `C:/tmp/xfw-readiness-*.log` on this workstation.
- Receipt artifacts: `src/Tests/Portal.E2ETests/bin/Debug/net10.0/receipt-artifacts` (generated, not committed). Temporary authenticated browser state and traces must not be committed or attached to a public report.
- Retained PDF-render evidence: [Classic 58 mm](receipt-classic58.png), [Bold 80 mm](receipt-bold80.png), [Compact 58 mm long receipt footer](receipt-long-footer.png).
- Retained live evidence: [Jay Test 01 warehouse](jay-warehouse.png). Temporary saved authentication and failure trace archives were removed after testing; the owned Docker test tunnel was stopped. Existing dev containers were left running and healthy.
- Relevant tests: `PosPersistenceIntegrationTests`, `PosReadinessRegressionTests`, `PosBoltRuntimeIntegrationTests`, `StockReservationTests`, `PosReceiptPrintBrowserTests` and the read-only `PosCashierE2ETests`/`PosListLayoutE2ETests`/`PosRegistersE2ETests`.

```powershell
# Requires reachable Docker for disposable PostgreSQL; use the documented remote test tunnel if needed.
dotnet test src/Tests/Inventario.IntegrationTests/Inventario.IntegrationTests.csproj -m:1 /nr:false
dotnet test src/Tests/POS.IntegrationTests/POS.IntegrationTests.csproj -m:1 /nr:false
dotnet test src/Tests/Inventario.Api.Tests/Inventario.Api.Tests.csproj -m:1 /nr:false
dotnet test src/Tests/POS.Api.Tests/POS.Api.Tests.csproj -m:1 /nr:false
# Live browser fixture variables are documented in src/Tests/Portal.E2ETests/POS-E2E.md.
dotnet test src/Tests/Portal.E2ETests/Portal.E2ETests.csproj --filter 'TestCategory=Module:POS|TestCategory=Module:Inventario' -m:1 /nr:false
```
