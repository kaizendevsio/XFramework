# Inventario and POS Production Readiness

Date: 2026-11-10 (Asia/Singapore).

## Release Decision

**NO-GO for unrestricted production rollout.** This pass found and fixed two POS defects and a known vulnerable dependency. The changes are local and have not been deployed. Passing service, browser, and disposable-database tests is not proof of production recovery, operations, or payment-provider readiness.

This supersedes the remaining-gate assessment in the [October verification](../2026-10-29-pos-inventory-verification/report.md), without replacing its historical transaction evidence. The [November UI retest](../2026-11-07-pos-dialog-sale-links/report.md) remains historical evidence for the dialog and sale-link fixes.

## Candidate and Test Boundaries

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

| Gate | What still blocks sign-off | Next verification / owner |
| --- | --- | --- |
| Candidate release | The fixes above are not in Xeon Dev or a release image; local passing tests do not replace CI. | Engineering: review/merge, complete required CI, build the immutable candidate, deploy to staging and rerun checkout/refund/cancellation/retry against that exact image. |
| Full-stack interruption recovery | Simulated lost replies and fresh DbContexts do not prove recovery after a real POS, Wallets, or Inventario process dies between commit and acknowledgment. | Engineering/operations: isolated full-stack tenant/environment; interrupt each phase, restart, retry the same keys, reconcile cash/wallet and stock ledgers, verify no duplicate settlement or stranded reservations. Do not interrupt the shared dev stack during other work. |
| Production recovery | Only a disposable POS database was dumped/restored. Production backup availability, encryption, retention, all-module restore and schema/image rollback are not demonstrated. | Operations: restore an actual sanitized backup, validate module relationships and totals, rehearse compatible image/schema rollback, record agreed RPO/RTO and measured times. |
| Production security/operations | Dev health and synthetic authorization checks do not validate production TLS, secret handling, actual role assignment, alerts or incident response. | Operations/security: verify production configuration, real restricted cashier/manager/inventory accounts, tenant isolation, alert delivery for failed settlement/reconciliation and a recovery runbook. |
| Capacity | The 20-reservation and 12-refund races check invariants, not sustained busy-shop operation. | Engineering/product: agree expected registers/traffic and latency/error targets; run sustained mixed checkout/refund/catalog traffic and verify final ledgers. |
| Advanced inventory transfers | Earlier UI testing found Transfer disabled with no usable source/destination workflow. Backend transfer tests passing do not close that UI gap. | Product/engineering: ship and test a supported source/destination UI, or explicitly exclude advanced transfer operations from the initial launch scope. |
| Physical POS hardware | Browser PDF/scan-search checks cannot verify printer drivers, paper feed/cut, USB/Bluetooth scanning or phone-camera pairing in the actual shop. | Store acceptance: test the intended 58/80 mm printers and scanners, including long receipts and repeated scans. Required if those devices are part of launch. |
| External payment settlement | No supported provider merchant/sandbox setup and real settlement/refund evidence in this pass. Internal wallets/cash are not gateway settlement. | Business/payment engineering: choose an eligible provider, configure secrets securely and test success, decline, timeout/webhook replay, refund and reconciliation. Can be excluded only for an explicitly cash/internal-wallet-only launch. |
| Business acceptance | Receipt content, tax/discount rules, returns policy and operational procedures need the business owner's approval for the intended launch. | Business/store owner: approve the exact receipt and configured rules on the release candidate. No regulatory certification is implied by these tests. |

An explicitly limited pilot may exclude external gateways and advanced transfers, but still needs the candidate, recovery, access-control and operational gates. Do not label untested items as passed merely because they require an environment, equipment or business decision.

## Evidence and Reproduction

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
