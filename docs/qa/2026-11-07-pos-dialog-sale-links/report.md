# POS Dialog and Sale Link Retest

Date: 2026-11-07. Environment: http://xeon-dev:5000. Tenant: Jay Test 01.

Status: UI-002 and UI-003 from the [earlier report](../2026-11-07-jay-pos-location-retest/report.md) are fixed, deployed and passed their targeted live retests. This does not expand the previous transaction test coverage to external payments or physical printing.

## Fixes

### UI-002: Clipped return and receipt dialogs

Confirmed before the fix at 1260 x 568: the return dialog measured 1024 x 714.5, with top -73.25 and bottom 641.25. Its computed maximum height was `none` and overflow was `visible`.

Added the POS-specific `pos-scroll-dialog` layout to the return form and receipt preview. It limits the dialog to the viewport minus 32 pixels, scrolls its body independently, and keeps header/footer from shrinking. Receipt preview also has an explicit footer close button. Other dialogs are not restyled.

Live DOM checks in [assert-dialog.js](assert-dialog.js) verify dialog bounds, independent body overflow, action bounds and unobscured action hit targets. All six combinations passed:

| Viewport | Return dialog | Receipt preview |
| --- | --- | --- |
| 1260 x 568 | Pass: height 536, top 16; body 370, scroll height 549. [Screenshot](screenshots/return-1260x568.png). | Pass: height 536, top 16; body 396, scroll height 544. [Screenshot](screenshots/receipt-1260x568.png). |
| 390 x 844 | Pass: width 358, height 812, top 16; footer visible. [Screenshot](screenshots/return-390x844.png). | Pass: width 358, height 724, top 60; footer visible. [Screenshot](screenshots/receipt-390x844.png). |
| 1920 x 1080 | Pass: width 1024, height 714.5; normal centered layout. [Screenshot](screenshots/return-1920x1080.png). | Pass: width 512, height 684; normal centered layout. [Screenshot](screenshots/receipt-1920x1080.png). |

Additional interaction checks passed:

- Reached and filled the return Reason field while the footer remained visible, then cancelled without submitting.
- Reopened the return dialog, opened Original Sale, and selected the existing receipt through the nested picker.
- Cancel dismissed the return dialog; Close preview dismissed receipt preview.
- Create Return stayed disabled for the already fully refunded receipt, preserving the over-return guard.

### UI-003: Sale direct links stuck loading

The page subscribed to tenant/module context notifications and started asynchronous reloads outside Blazor's UI-event lifecycle. It changed `_loading` but did not request a render when that reload completed. On direct entry, module initialization can supersede the lifecycle-triggered load; its later context-triggered load then leaves the displayed skeleton unchanged.

Added explicit rendering at reload start and completion. Existing load-version and tenant checks continue to discard stale responses; disposed pages do not request renders.

Live checks on sale `c5504146-36c3-450e-9440-58ece752f4a8`:

| Entry path | Result |
| --- | --- |
| Direct hard navigation | Pass: rendered receipt totals, both line items and payment audit without a Sales-list detour. [Screenshot](screenshots/sale-direct-short.png). |
| New browser tab | Pass: Jay Test 01 restored and sale rendered. [Screenshot](screenshots/sale-new-tab.png). |
| Browser reload | Pass: DOM contains `pos-sale-detail`, the expected sale number and Jay Test 01. |
| Invalid sale ID | Pass: rendered Sale unavailable and Back to Sales rather than an indefinite skeleton. [Screenshot](screenshots/sale-invalid-link.png). |

No uncaught browser JavaScript errors were observed in the final check.

## Regression Tests

Added `PosSaleDetailLoadingTests`: delayed context-event success, delayed error, tenant clear during an in-flight response, and module revocation after a rendered sale. Before the implementation fix, three of these four tests failed; afterward all four passed.

Focused run: 16 passed, 0 failed, 0 skipped.

```powershell
dotnet test src/Tests/Portal.E2ETests/Portal.E2ETests.csproj -m:1 /nr:false --filter 'FullyQualifiedName~PosSaleDetailLoadingTests|FullyQualifiedName~PosSaleReceiptPrintingTests|FullyQualifiedName~DialogStackingContractTests|FullyQualifiedName~LocationDetailLoadingTests' --logger 'console;verbosity=minimal'
```

Broader Portal contract run: 459 passed, 0 failed, 1 skipped. The skipped test was `RedisTicket_ReloadedApplicationAndProtectionProvider_RetainsRotatedCredential`, which requires its Redis integration setup.

```powershell
dotnet test src/Tests/Portal.E2ETests/Portal.E2ETests.csproj --no-build -m:1 /nr:false --filter 'TestCategory=Area:PortalContract' --logger 'console;verbosity=minimal'
```

Release publish succeeded with existing warnings. `git diff --check` passed. The geometry assertions above were run against the deployed UI, not mocked component markup.

## Deployment

- Image: `xframework-portal:pos-ui-fix-20261107`, derived from `xframework-portal:location-fix-20261107`.
- Container: `xframework-portal`, healthy after recreation and at the final check.
- Override: `/home/xeon/pos-ui-fix-20261107.override.json`.
- Existing release: `/home/github-runner/xframework-deploy/releases/39825885286-1`.
- Replaced only the POS feature DLL/PDB, app.css and its compressed copies, and the stylesheet's generated static-asset endpoint metadata. Other deployed asset endpoints were preserved.
- Browser verified the new fingerprinted stylesheet URL: `css/app.fti9jom7nd.css`.
- Local/container POS DLL SHA256 matched: `a0eb5bd1ea3beba9584283fcdf4798a2cd2a5ceac04d3b79e6e0373bc65fbb68`.
- Local/container app.css SHA256 matched: `833a87f0c51e6a8967ab084fa0da0360576b86b6fa289456717a990ee845990c`.

No backend restart, schema migration, tenant feature change, new sale or new refund. The original fully refunded QA sale was reused for this UI verification. Prior warehouse/location fixes remain in the parent image. Source changes are local; no PR merge was requested or performed.

Rollback: use the existing Compose override chain through `location-fix-20261107.override.json`, omit the final POS UI override, and recreate only Portal.
