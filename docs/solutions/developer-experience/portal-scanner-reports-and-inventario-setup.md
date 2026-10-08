---
title: "Portal Scanner, Reports, and Inventario Setup"
date: 2026-10-07
category: developer-experience
module: Portal
problem_type: workflow
component: portal_features
severity: high
applies_when:
  - "Deploying or testing phone pairing, inventory setup, or Portal reports"
tags: [portal, pos, scanner, reports, inventory, finance, onboarding]
status: current
---

# Portal Scanner, Reports, and Inventario Setup

## Phone Pairing And Fullscreen

The pairing dialog has a six-digit manual code and a countdown. Its main QR retains
a high-entropy one-time challenge; the Phone scanner popover opens the authenticated
scanner landing page without putting that challenge in the URL. Claiming requires
the same credential and effective tenant, but a different phone session.

Manual claims require distributed attempt limits. Configure
`PosScanner:RedisConnectionString` in POS; Compose supplies it from
`POS_SCANNER_REDIS_CONNECTION` (default `redis:6379`). Missing or unavailable Redis
fails manual claims closed; it does not bypass limits. QR pairing remains available.
Codes are reserved through the pairing's sliding expiry to prevent reassignment.
Authorized desktop polling keeps claimed pairings alive across customers for an
entire shift; the phone no longer displays an active-pairing expiry. Logout, an
explicit disconnect, tenant/register changes, or a lost desktop lease end pairing.
Old-sale queued scans are discarded when the cart is completed, cleared, or replaced.
The phone has one camera control and collapsed manual-code entry, with a compact
disconnect action rather than a full-width pairing button.

Cashier focus requests native browser fullscreen during the originating click.
Escape and native fullscreen exit restore the normal layout. If fullscreen is
unsupported or denied, the focused layout remains usable.

Physical-phone camera validation remains a rollout check. Use a secure browser
origin for camera access; the existing plain HTTP dev address is not a guarantee
that a mobile browser will grant camera permission.

## Reports

- Inventario: `/inventario/reports`, backed by the reporting snapshot operation.
- Finance: `/finance/reports`, backed by the Wallets financial report operation.
- Live mode refreshes every 30 seconds; it is polling, not a pushed event stream.
- Share links preserve filters and tenant scope, not authorization. Recipients
  must authenticate and satisfy the normal tenant, feature, and actor permissions.
- PDF export uses the currently authorized snapshot and includes vector charts.
  jsPDF, AutoTable, and the font are bundled locally with their license files.

Periods use UTC, inclusive start and exclusive end, with a maximum of 366 days.
Inventory totals cover the full filtered scope; detail sections are capped at
1,000 rows and identify that cap. Reservation and release entries remain in movement
history but are excluded from physical inbound/outbound totals.

Finance covers current wallet balances and posted wallet activity by currency,
not profit-and-loss accounting. Holds and external counter-entries are not posted
activity. Legacy ledger entries without a currency use their authorized wallet's
currency; unconfigured wallet currencies fail instead of combining money into an
unspecified total. Standalone walletless fee entries are not a separate fee total.

## Product QR Labels

Products exposes **Print labels** for the currently loaded/filtered catalog (up to
250 products, not an unlimited whole-catalog export). The dialog uses `BbDataGrid`
selection and filtering, previews the first selected QR, and exports up to 500
labels with product name and SKU. Products need a nonempty printable SKU of at most
256 characters; no automatic SKU mutation or new product-code format is introduced.

QRs encode the exact product SKU used by POS lookup. Variation-specific identifiers
are not available in the current catalog; these are product labels, not distinct
variant labels. If a SKU matches several sellable variations, the cashier's normal
selection workflow still applies.

Paper options are A4 cut-out sheets (10 mm margins, 2 mm gaps) and single-label rolls.
Width/height and copies are configurable; pre-cut stock with a different pitch must
be matched in the printer workflow. Print opens the native PDF viewer with a print
request; Download PDF remains available when browser pop-ups are blocked. Print at
actual size, not fit-to-page. No physical printer or sticker-stock calibration is
implied by the browser tests.

Barcode encoding/decoding and its pinned, licensed WASM assets live in Portal Shared;
POS and Inventario reuse them. PDF generation reuses the local jsPDF and Unicode
font loader. Exports use the selected authorized UI snapshot, without new queries.

## Inventario Setup

Apply migration `20261007050000_AddInventarioSetup` through the normal deployment
migration path. Setup state belongs to the Inventario schema and is unique per
tenant; existing tenants are not automatically rewritten.

A fresh tenant administrator visiting Products is directed to `/inventario/setup`.
Basic setup creates one confirmed warehouse/location and saves currency and
low-stock defaults. Advanced setup can select existing storage or explicitly create
new storage. Confirmation is atomic and replay-safe. Neither path seeds products,
stock, wallets, or funds. Existing tenants and ordinary users do not get the
automatic prompt. Preferences remain editable in Inventario Settings.

## Verification Boundaries

Portal component browser tests cover desktop/mobile layouts, wizard interaction,
pairing focus and expiry, fullscreen behavior, protected share scopes, and PDF
generation. Backend integration tests use isolated PostgreSQL/Redis containers
and real generated Bolt wrappers, including setup migration, authorization,
concurrency, replay, currency separation, and pairing quotas.

These checks do not constitute a deployed-stack or physical-phone camera test.
Do not submit sales, change stock, or move funds in an existing tenant just to
inspect scanner or reporting UI.
