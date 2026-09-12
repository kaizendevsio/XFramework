# Final Inventario acceptance follow-up

Base: `10e2cc8d` on develop. This batch addresses the final parent acceptance findings after PR #452. Parent QA owns live fixtures, coordinated deployment, and deployed acceptance. This implementation task performed no live business writes.

## Changes

- Active reservations now have the same named View action as terminal reservations. Detail shows base/variant identity, current on-hand/reserved/available quantities from the loaded balance, and exact stock-position links in the detail and allocation grid. Variant text uses the existing display helper with one tenant-scoped exact-ID lookup.
- Reservation quantity validation uses the existing eligible-stock calculation. Zero or over-available quantities disable Reserve and explain the correction. Known server business failures and HTTP statuses map to actionable messages in the dialog; arbitrary backend details are not displayed, and failed drafts remain intact.
- Product-context movements disable zero quantities and missing product/location context. The form shows selected-position availability and explains signed adjustments and reserved/on-hand effects. Posting still uses the existing wrapper without business-rule changes.
- Audited all 26 Inventario grids with detail `OnRowClick`. Twelve lacked a native detail action: three in LocationDetail, two in LotDetail, two in ProductDetail, one in PurchaseOrderDetail, one in StockBalanceDetail, and three in WarehouseDetail. Existing identifiers now link to the same detail route. Existing actions stay in place; filtering/sorting remain native. A contract test guards all row-click detail routes.
- Category/product, supplier/order, and product/stock paths carry their existing GUID query scope through both row and explicit View navigation and the return path. Product sidebar sections retain category scope. Category filtering is visible and clearable, and query changes reapply the catalog filter.

## Verification

- Portal nonbrowser suite: **107 passed**, including 11 new navigation, quantity, eligible-stock, and safe failure-message cases. Separate credential-dependent browser fixtures were excluded, including the newly merged POS list-layout tests. See `portal-tests.txt`.
- Inventario Portal contracts: **16 passed**, including the new native detail-action contract and the updated category-aware sidebar contract. See `contract-tests.txt`. These source contracts use the test project's PostgreSQL fixture; no live application data was touched.
- Portal build: passed with 60 existing warnings and no errors. See `portal-build.txt`.
- `git diff --check`: passed.

Browser checks used actual compiled feature components and the actual host ProductDetailSidebar in an isolated local host, with synthetic fixture state and business calls blocked. The host reacts to real navigation events; it does not substitute copied grid or form markup.

1. Active base and variant reservations and a released reservation expose named View buttons. Focus + Enter opens the correct reservation route. Base detail displays `10.00 / 2.00 / 8.00`; variant detail displays `8.00 / 1.00 / 7.00` and `Size: QA Medium`. Both detail and allocation stock-position links navigate by keyboard to their exact base/variant balance routes. Terminal detail retains no Release/Fulfill/Cancel actions. See `reservation-list.txt`, `reservation-base.txt`, `reservation-variant.*`.
2. Product movement starts at zero with Post Movement disabled. In the selected base position, availability is 8. A signed `-1` adjustment is retained and enables Post Movement; the draft was cancelled. Numeric values were entered as a single keyboard insert to avoid asynchronous per-keystroke fixture races. See `product-movement-zero.txt`, `product-movement-negative.txt`, and `product-movement-adjustment.png`.
3. Product Lots exposes the named `View lot: LOCAL-LOT` native link; focus + Enter navigates to its existing lot detail route. See `product-lot-link.txt`. The local fixture does not render the destination lot-detail page.
4. Category detail's `1 products` link opens the filtered catalog with the category label and clear link. Keyboard View product retains category scope, the actual sidebar Stock link retains it, and Product List returns to the same one-row catalog. Clearing the category displays both fixture products. See `category-return.txt`.
5. Supplier-scoped orders show only `LOCAL-PO`. Keyboard View purchase order carries `supplierId`; Back to purchase orders restores the same scoped list and clear control. See `supplier-return.txt`.
6. Product-scoped stock shows exactly the base and variant positions. Keyboard View stock balance opens the variant balance with `productId`; Back to stock returns to the same two positions and visible product scope. See `stock-return.txt`.
7. With eligible base availability 10, entering quantity 11 retains the draft, shows the actionable inline error, and disables Reserve. The draft was cancelled without a wrapper call. See `reservation-overlimit.*`.

The final screenshots were visually checked. This evidence verifies local rendering and navigation; parent QA will repeat the relevant paths after the coordinated deployment.
