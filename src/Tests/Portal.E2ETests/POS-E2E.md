# POS Cashier Browser Tests

`PosCashierE2ETests` is the NUnit/Microsoft.Playwright browser acceptance suite for
`docs/plans/2026-09-10-pos-cashier-ux.md`. It uses the existing Portal test project
and an externally started Portal. It does not start hosts, seed records, alter
runtime configuration, or change database state through an API helper.

`PosRegistersE2ETests` covers register-list navigation and the dedicated detail/edit workflow.
It uses only `POS_E2E_BASE_URL`, authentication/storage state, `POS_E2E_TENANT_ID`, and
`POS_E2E_REGISTER_NAME`. The test enters edit mode, submits an invalid local draft, discards it,
and never sends a valid register update or changes fixture data.

Its explicit save/reload case changes only the selected register description, verifies the saved
value after a fresh wrapper read, and restores the original description in `finally`. Run it only
against a disposable tenant with the two opt-in guards shown below.

## Prerequisites

- .NET 10 SDK, the existing restored NuGet packages, and Playwright Chromium.
- Portal at `http://127.0.0.1:5267` by default, with its authorized module connections working.
- An authorized Portal account/session with a cashier credential and access to the chosen tenant.
- POS sales and carts features enabled; an enabled register with a configured currency and
  valid cash drawer wallet so the payment-readiness assertions can become enabled.
- Two available, positively priced catalog entries: a base product and a distinct variant.
  The base-product SKU must resolve uniquely; the variant SKU is expected to match two entries
  by default, exercising safe ambiguous-SKU scanning. Product-name and variant queries must not
  themselves equal a SKU, since an exact SKU search can auto-add an item.
- A populated default register catalog, because initial loading is asserted before selecting
  the configured register. Use existing `E2E Inventory POS 20260906` fixture data where available;
  names, IDs, prices, and credentials are deliberately not hardcoded in these tests.
- The independent-scroll case uses a short, narrow desktop viewport (1100x500) and the existing three
  catalog entries (water, shirt, and a shirt variant). It adds three distinct entries by default
  and requires natural overflow in **both** panes, without injecting DOM rows or changing CSS.
  `POS_E2E_SCROLL_ITEM_COUNT` (2-24) can target a different already-provisioned fixture.
  Too few entries or a non-overflowing pane fails with a fixture explanation; it is not skipped.

Each test gets a fresh browser context and unsaved draft. Normal cases only search, add/remove
draft items, edit quantities/payment fields, and open/close dialogs. They never click Hold sale,
Resume, Cancel, or submit Pay. Login can create normal authentication/session/audit records.
Tests require real products and endpoints; no catalog or Blazor traffic is mocked.

## Configuration

Set environment variables in the shell running `dotnet test`. Keep secret values and session
files out of source control. Missing authentication/data configuration fails the selected tests,
rather than passing or calling `Assert.Ignore`.

| Variable | Value |
| --- | --- |
| `POS_E2E_BASE_URL` | Optional; defaults to `http://127.0.0.1:5267`. Use the same hostname as the saved session. |
| `POS_E2E_STORAGE_STATE` | Absolute path to an authorized Playwright storage-state JSON. Takes precedence over login credentials. |
| `POS_E2E_USERNAME` / `POS_E2E_PASSWORD` | Required when storage state is not supplied. Uses the real `/login` form with a direct cashier return URL; waits for the redirected cashier without an intermediate navigation. |
| `POS_E2E_TENANT_ID` | Required existing tenant GUID. Sets the Portal's normal active-tenant local-storage preference; does not bypass authorization. |
| `POS_E2E_REGISTER_NAME` | Required exact picker display label, including ` (CODE)` when displayed. |
| `POS_E2E_CURRENCY` | Required displayed register currency code, e.g. `PHP`. |
| `POS_E2E_PRODUCT_NAME` | Required exact base-product display name, as in the tile's `Add ...` accessible name/cart row. |
| `POS_E2E_PRODUCT_SKU` | Required unique sellable base-product SKU. |
| `POS_E2E_VARIANT_NAME` | Required complete variant display name, as in the tile's `Add ...` accessible name/cart row, not only the variant suffix. |
| `POS_E2E_VARIANT_QUERY` | Required text found in the variant's name; must find the configured variant without exact-SKU auto-add. |
| `POS_E2E_VARIANT_SKU` | Required variant lookup SKU, different from the uniquely scanned base-product SKU. May be shared by another product/variant. |
| `POS_E2E_VARIANT_SKU_MATCH_COUNT` | Optional expected exact-SKU match count, defaults to `2`. Multiple matches must remain visible without auto-add. Set `1` for a unique variant SKU, which must auto-add exactly once. |
| `POS_E2E_SCROLL_ITEM_COUNT` | Optional; defaults to `3`, range `2-24`. See scroll prerequisites above. |
| `POS_E2E_IGNORE_HTTPS_ERRORS` | Optional; `1` only for an explicitly trusted development certificate. Default is strict HTTPS validation. |
| `POS_E2E_ARTIFACTS_DIR` | Optional artifact root; defaults to `TestResults/pos-cashier` under the NUnit work directory. |

The browser locale is `en-US`; money assertions expect the Portal's two-decimal display with
`.` decimal and optional `,` grouping. Light/dark tests set the existing `bb-theme` preference
before page initialization, then verify the actual theme class. No alternate theme system is used.

## Run

From the repository root, with the fixture environment already populated:

```powershell
dotnet build src/Tests/Portal.E2ETests/Portal.E2ETests.csproj -m:1 /nr:false
pwsh src/Tests/Portal.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium

dotnet test src/Tests/Portal.E2ETests/Portal.E2ETests.csproj --no-build `
  --filter 'FullyQualifiedName~Portal.E2ETests.PosCashierE2ETests&TestCategory!=FinancialMutation' `
  --logger 'trx;LogFileName=pos-cashier.trx' `
  -- Playwright.BrowserName=chromium
```

Run the read-only register workflow separately:

```powershell
dotnet test src/Tests/Portal.E2ETests/Portal.E2ETests.csproj --no-build `
  --filter 'FullyQualifiedName~Portal.E2ETests.PosRegistersE2ETests' `
  -- Playwright.BrowserName=chromium
```

Opt in to the isolated real update regression:

```powershell
$env:POS_E2E_ALLOW_REGISTER_UPDATE = '1'
$env:POS_E2E_ISOLATED_TENANT_ID = $env:POS_E2E_TENANT_ID
dotnet test src/Tests/Portal.E2ETests/Portal.E2ETests.csproj --no-build `
  --filter 'FullyQualifiedName=Portal.E2ETests.PosRegistersE2ETests.Register_IsolatedDescriptionUpdate_PersistsAfterReloadAndRestoresOriginal' `
  -- Playwright.BrowserName=chromium
Remove-Item Env:POS_E2E_ALLOW_REGISTER_UPDATE
Remove-Item Env:POS_E2E_ISOLATED_TENANT_ID
```

Append `Playwright.LaunchOptions.Headless=false` after `--` to watch the run. To use installed
Chrome instead, also supply `Playwright.LaunchOptions.Channel=chrome`.

For one case, filter by its full method name. For example:

```powershell
dotnet test src/Tests/Portal.E2ETests/Portal.E2ETests.csproj --no-build `
  --filter 'FullyQualifiedName~PosCashierE2ETests.Catalog_RapidConsecutiveQueries' `
  -- Playwright.BrowserName=chromium
```

Keep browser tests out of the fast, non-browser Portal contract run:

```powershell
dotnet test src/Tests/Portal.E2ETests/Portal.E2ETests.csproj --no-build `
  --filter 'FullyQualifiedName!~Portal.E2ETests.PortalE2ETests.&TestCategory!=Kind:E2E'
```

Do not run the whole existing `PortalE2ETests` browser fixture against shared data; that older
fixture performs unrelated tenant/user mutations and does not use this suite's configuration.

## Real Checkout Opt-In

`Checkout_IsolatedCashSale_CompletesAndShowsReceipt` is `[Explicit]`, separately categorized
`FinancialMutation`, and guarded by two environment checks before it clicks Pay. Run it only
with explicit authorization for a disposable isolated tenant/register/product. It creates a
real sale and may post inventory/wallet/ledger data; no automatic cancel, return, refund, or
database cleanup is attempted. Keep the emitted receipt number for reconciliation. A failed
or timed-out submission must be investigated before rerunning; there is no automatic retry.

```powershell
$env:POS_E2E_ALLOW_CHECKOUT = '1'
$env:POS_E2E_ISOLATED_TENANT_ID = $env:POS_E2E_TENANT_ID
dotnet test src/Tests/Portal.E2ETests/Portal.E2ETests.csproj --no-build `
  --filter 'FullyQualifiedName=Portal.E2ETests.PosCashierE2ETests.Checkout_IsolatedCashSale_CompletesAndShowsReceipt' `
  -- Playwright.BrowserName=chromium
Remove-Item Env:POS_E2E_ALLOW_CHECKOUT
Remove-Item Env:POS_E2E_ISOLATED_TENANT_ID
```

Selecting this method without the guards fails, rather than silently skipping it. A completed
receipt is the browser assertion; financial posting/idempotency correctness remains the domain
integration suite's responsibility.

## Selector Contract

Prefer accessible controls for actions. Test IDs are only for ambiguous values and layout regions:

| Hook | Expected element/semantics |
| --- | --- |
| `pos-cashier` | Cashier root; only present after tenant/module setup succeeds. |
| `pos-catalog` | Catalog region with `aria-busy="true|false"` and `data-search-query` containing the last successfully applied, trimmed query (empty for initial catalog). Idle waiting also accepts Razor's omitted-false Boolean attribute, but not an empty/true busy attribute. |
| `pos-register-picker` | Wrapper containing the `Register` button. |
| `pos-catalog-empty` | Visible no-match/no-products message wrapper, absent or hidden when results exist. |
| `pos-catalog-scroll`, `pos-cart-scroll` | The actual independent vertical scrollers, not an outer wrapper. |
| `pos-product` / `.pos-product-tile` | Catalog button named `Add {DisplayName}`. Product/variant text may be split visually. |
| `pos-cart-line` / `.pos-cart-line` | One product/variant row; `pos-cart-item-name` contains its full display name. |
| `pos-subtotal`, `pos-discount`, `pos-tax` | Displayed adjustment breakdown amounts; may be absent when both adjustments are zero. |
| `pos-total`, `pos-change` | Displayed two-decimal numeric values. |
| `pos-cash-preset` | Each common-amount button. Its displayed money value is the absolute tender amount it sets. |
| `pos-checkout` | Pay button, not its surrounding container. |
| `pos-receipt` | Completed/recovery receipt region, including sale number, status, and amount. |

The search input retains `id="pos-catalog-search"` and accessible name `Search products`;
guidance retains `id="pos-checkout-status"`. Cash received uses its associated label. Quantity
and money helpers use `.pos-qty-value`, `.pos-cart-line-main > span`, and `.pos-cart-line-total`.
Dialogs are named `Sale details`/`Held sales`; the details textarea is labeled `Sale notes`.

The existing shared `XfEntityPicker` renders nested buttons with duplicate accessible names.
Register/Customer helpers deliberately target `button.xf-entity-picker-trigger` to avoid a strict
locator collision. This is a known shared-component semantic limitation, not an assertion that
its current accessibility markup is correct; fixing that component is outside this suite's scope.

## Coverage And Artifacts

- Initial catalog before register selection; product name/variant query, no match, clearing search.
- Unique base-product SKU scans, duplicate scan quantities, and focus restoration; ambiguous
  variant SKU search shows all exact matches without auto-add (or verifies a configured unique SKU).
- Overlapping Enter searches with no artificial inter-query delays and no stale final results.
- Add, increment/decrement, removal and undo restoring row order, quantity, and computed totals.
- Discount/tax arithmetic and negative-total readiness rejection.
- Insufficient cash, exact amount, excess/change, common tender buttons; wallet needs customer.
- Three consecutive details/held-sales dialog cycles without persisting the draft.
- 1920x1080, 1366x768, 768x1024, and 390x844 in both themes, normal/focus modes,
  container overflow checks and a trial-only Pay click to verify reachability.
- Independent keyboard-focused catalog/cart scrolling at 1100x500 with three real entries and payment visible.
- Page errors, console errors, visible Blazor error/reconnect UI fail the test.

Failed browser tests attach `page.png`, `page.html`, `browser.txt`, and `trace.zip` to NUnit/TRX.
The trace starts after credential login and is discarded on success. Open a failed trace locally:

```powershell
pwsh src/Tests/Portal.E2ETests/bin/Debug/net10.0/playwright.ps1 show-trace '<absolute-path-to-trace.zip>'
```

Artifacts and storage-state files can contain tenant/customer data, session cookies, and authenticated
traffic. Username/password strings are redacted in textual artifacts, but trace ZIPs are not a
general secret-redaction mechanism. Store them in restricted locations, do not commit them, and
do not upload them to public trace viewers. Authentication/configuration failures before tracing
cannot have a trace; the runner reports the setup failure directly.
