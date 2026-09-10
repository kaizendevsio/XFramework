# POS Cashier UX And E2E Plan

Base: latest origin/develop at d5b6c091. Branch: codex/pos-cashier-ux-e2e.

## Outcome

Make the cashier follow product selection, cart verification, and payment. Keep existing POS wrappers, tenant authorization, price validation, stock posting, and idempotency authoritative.

## Implementation

- [x] Repair initial catalog loading, text/Enter/SKU search, request concurrency, and recoverable error handling. Reverify the observed stale searches and circuit failure rather than assume a backend cause.
- [x] Move register selection into the header and put a readable, independently scrolling cart above payment. Keep totals and the payment action visible.
- [x] Fix product tile/availability overflow, show variant names clearly, simplify metadata, and preserve product images and accessible fallbacks.
- [x] Show register currency, explicit discount/tax amounts, checkout readiness, exact/common cash amounts, and change. Use Cash, Customer wallet, Customer, Hold sale, and Held sales terminology.
- [x] Reduce nested borders and destructive visual emphasis, provide undo for a removed draft item, and add an optional cashier focus view.
- [x] Add focused automated regression and browser E2E coverage using the existing NUnit/Playwright projects.
- [x] Fix browser-proven test prerequisites: disable login fields until Blazor hydration completes so fast typing is not lost, and serve the Portal favicon instead of returning 404.
- [x] Build and run relevant tests; run locally and verify in Chrome at desktop, tablet, and mobile widths in light/dark themes.

## E2E Acceptance

- Initial catalog renders without a manual search; product names, SKU and variant lookup work; no-match queries show an explicit empty state.
- Enter and fast consecutive searches use current input and do not show stale results or terminate the Blazor circuit.
- Adding products and variants, increasing/decreasing quantity, removing and undoing an item produce correct totals.
- Cash payment explains insufficient tender, supports exact/common amounts, and computes change; wallet payment requires a customer; unavailable prerequisites prevent checkout with a reason.
- Hold/details dialogs open and close repeatedly, preserve draft contents, and use consistent terminology. Financial submission tests must use explicitly configured isolated test data.
- Product labels/badges and cart rows stay inside their containers at 1920x1080, 1366x768, 768x1024, and 390x844. Cart/catalog scroll independently where side-by-side and primary payment remains reachable on narrow screens.
- No unexpected page errors or visible Blazor circuit error. Screenshots/traces accompany failed browser tests.

## Verification Record

- Deployed crash verified in logs: clearing search set its bound value to null; `Trim()` threw before the catalog RPC. Search is now null-safe and reads the current input on submission.
- Backend catalog filters reproduced correctly with PostgreSQL; no filter/cache defect found. Added cancellation forwarding to both downstream catalog wrapper calls.
- Final Portal build passed with zero errors and seven existing warnings (duplicate imports and obsolete dialog confirmation calls). E2E project build passed without warnings/errors. POS Portal contracts plus catalog service/serialization tests: 22 passed. Portal non-browser contracts: 76 passed.
- New catalog PostgreSQL tests: 15 passed. Existing POS API tests: 30 passed. A broader existing Bolt fixture failed database authentication (`28P01`) during setup; not caused by the cashier changes.
- Local preview: http://127.0.0.1:5267/pos/cashier. Live/readiness endpoints return 200. Uses authorized HTTPS/WSS connections to Xeon Dev; no deployed containers or registration changed.
- Manual Chrome visual checks completed at the four planned viewports, including light/dark and focus mode. Fixed the mobile header wrapping conflict discovered during these checks.
- All 20 distinct non-financial Chrome E2E cases passed across targeted batches and reruns, not one uninterrupted green run. Coverage includes search/scan races, cart/undo, cash and wallet readiness, adjustments, repeated dialogs, independent scrolling, and eight viewport/theme combinations with focus mode.
- Earlier batches encountered existing Bolt disconnects during search or authentication setup. The retryable catalog error preserved the circuit; affected setup cases passed on rerun. No security quotas or backend connection settings were weakened.
- Browser tests exposed and verified fixes for dialog close focus restoration, immediate Escape handling while Blueprint finishes its asynchronous listener setup, and short-screen header wrapping that clipped payment. Keyboard scroll assertions wait for scrolling to settle; async radio assertions wait for Blazor's checked state.
- Ignored local TRX evidence is under `src/Tests/Portal.E2ETests/TestResults`: `pos-cashier-catalog-cart-cash.trx`, `pos-cashier-layout-chrome.trx`, `pos-cashier-workflows-chrome.trx`, `pos-cashier-final-workflows.trx`, `pos-cashier-final-dialog-scroll.trx`, and `pos-cashier-dialog-final.trx`. These together contain all 20 passing cases and retain the earlier failures rather than hiding them. Final non-browser Portal contracts are in `portal-contracts-completed.trx`.
- Real financial submission remains opt-in and was not run against shared test data.
- No PR, merge, or deployment was performed for this task. The original worktree and its existing changes were preserved; this implementation is isolated on the branch above.
