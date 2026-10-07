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
Codes are reserved through the pairing's hard expiry to prevent reassignment.

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
