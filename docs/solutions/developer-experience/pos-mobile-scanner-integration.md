# POS Mobile Scanner Integration

## Parent Cashier Hook

The scanner is independently scoped to this component instance, the selected
register, trusted tenant, cashier actor, and desktop login session. No Cashier
edits are included in the scanner branch.

```razor
@using XFramework.Portal.Features.POS.Scanner

<CashierScannerPairing
    RegisterId="@(TryResolveSelectedRegisterId() ?? Guid.Empty)"
    Disabled="@(_paymentOpen || IsInteractionLocked)"
    ScanReceived="ReceiveMobileScan" />

@code {
    private async Task ReceiveMobileScan(string code)
    {
        if (_paymentOpen || IsInteractionLocked)
            throw new InvalidOperationException("Cashier is busy");

        _catalogSearch = code;
        _selectedCategoryId = null;
        await SearchCatalog(addExactSkuToCart: true, readCurrentInput: false);
        if (_catalogError is not null)
            throw new InvalidOperationException(_catalogError);
    }
}
```

`ScanReceived` receives only a product code. A normally completed callback
acknowledges acceptance or a deliberate catalog rejection; throwing disconnects
the scanner without automatic redelivery, so check the cart before reconnecting.
The lookup currently catches failures internally. The `_catalogError` guard is
mandatory: a caught transport/catalog failure must not be acknowledged as success.
Any replacement hook must expose a result or throw on lookup failure.

`Disabled` pauses delivery; it does not disconnect. Paused polls renew the desktop
lease without draining or acknowledging the queue. New phone sends receive 423
(desktop busy), retain one pending code locally, and require explicit retry.
The component also checks `Disabled` immediately before each callback. Codes already
queued before the modal opened remain pending until it closes. If checkout should
discard those codes, remove/remount the pairing component after completed checkout
(for example with a cashier-sale-generation `@key`); disposal revokes the old pairing.
Register and tenant changes always revoke the old pairing.

Place the always-mounted component in `cashier-header-actions`. It renders one
compact icon+text button ("Scan with phone" / "Phone connected") and a hidden
accessible live region, not a permanent panel. The button opens a standard
controlled `BbDialog`; polling and pairing state live on the outer component.
Closing Done, Escape, or the dialog close control does **not** revoke pairing.
Disconnect, component disposal, tenant change, and register change do.

## Phone Entry

Open `/pos/mobile-scanner` on the same authorized HTTPS Portal origin. The phone
must sign in independently as the same cashier, not reuse/export a desktop cookie.
After login, scan the desktop pairing QR inside the scanner, or open its QR link
and press Pair desktop. If login redirects discard the URL fragment, open the
QR again after signing in. The route uses its own compact layout and manifest.
No new application, authentication flow, or framework is introduced.

The QR contains only a random, single-use pairing challenge in a URL fragment.
It contains no actor, desktop key, phone key, access token or session ID. When the
desktop has a selected tenant, the URL query includes that non-secret tenant ID
as a hint; the phone uses it in request metadata. It is never authorization.
For manual pairing, enter the same target tenant ID if operating outside your
login tenant. All six endpoints use existing trusted delegated-tenant policy;
cross-tenant access requires `IdentityTenantsManage` plus POS sales view/create.
Feature gates and pairing ownership use the trusted effective tenant. The cashier
credential is never substituted. Both devices must be independently authorized
for that target tenant; a forged hint or different cashier cannot claim/send.
Manual pairing-code entry is available if QR rendering is unavailable. Product
QR data is treated as a product code, never navigated/executed.

## Secure Origin Configuration

An HTTP desktop can pair to a separately configured HTTPS origin of this same
Portal app. Host configuration (normal IConfiguration/environment binding):

```text
Portal__ScannerPublicBaseUrl=https://xeon-dev.tailed40e.ts.net:5000
```

Equivalent appsettings:

```json
{ "Portal": { "ScannerPublicBaseUrl": "https://xeon-dev.tailed40e.ts.net:5000" } }
```

The configured value must be an absolute HTTPS root origin without userinfo,
query, fragment, or path prefix. If absent, an HTTPS desktop origin is used;
HTTP desktop fallback is denied with a meaningful configuration error.
Both the QR and Phone scanner link use the secure origin. The phone logs in at
that host independently; there is no transfer of desktop cookies or tokens.
No cookie domain, SameSite, session, refresh-token, or auth infrastructure change
is needed or included. Normal wrappers reuse existing cached service/actor tokens.

Current user-verified tailnet Serve handlers cover 443->8188, 5188, 7000, 8261,
and 8443; none currently serves Portal HTTPS. At authorized rollout, the parent
may add a targeted **tailnet-only** HTTPS Serve listener on 5000 to the existing
Portal listener, verify its certificate and access from the phone, and set the
above host environment value. Do not overwrite existing handlers or public
Funnel. No Serve configuration or deployment was changed by this branch.
No browser flags or insecure-origin exceptions are required or permitted for
camera acceptance. A trusted certificate and supported HTTPS browser are required.

## Catalog Semantics

Inventario's current Product/IProduct and sellable catalog contracts expose
`SKU`, not a dedicated barcode/EAN/GTIN field. The decoder reads QR, EAN-13,
EAN-8, UPC-A/UPC-E, Code 128, Code 39, ITF and Data Matrix, but automatic cashier
addition succeeds only when the decoded text is the one exact available SKU.
Retail barcode digits must be stored as the SKU to match. Arbitrary URLs remain
text and cannot navigate, set prices, or trigger financial submission.
ZXing's EAN/UPC family normalizes UPC-A into EAN-13 text (a leading zero),
and may expand UPC-E. Store that decoded canonical text as the SKU, or use a QR/
Code 128 carrying the exact SKU. No alternate-SKU guessing or price payload
interpretation is performed.

## Coordination And Limits

Pairing challenges expire after two minutes and are consumed once. Pairings have
a thirty-minute hard expiry, and require a desktop heartbeat within thirty seconds.
Disconnect revokes immediately; failed revocation/logout loses its desktop lease
within thirty seconds. A component-specific random desktop key isolates tabs even
when their login session is shared. Phone keys are bound to the independent phone
login session. None of these keys is persisted in local storage or embedded in QR.

Each pairing queues at most 32 codes and accepts at most 1,024 sequences; at most
eight active pairings per tenant/cashier and 4,096 globally are retained. A retry of
the latest identical sequence/payload is idempotent; stale, conflicting or skipped
sequences fail. Distinct sequences carrying the same product both count, even
immediately. Camera stability suppression rearms after 700 ms without a symbol,
so remove the item from frame between intentional scans. No offline sale or scan
queue is created. Only one pending phone code can be explicitly retried.

The in-memory coordinator is deliberately ephemeral and fails closed on restart.
Deploy on one POS API instance, or guarantee both device requests route to that
same instance. Multi-instance distribution is not implemented; do not roll this
out across independent replicas without shared coordination. No POS, Inventario,
Wallets, payment, stock, auth-cookie or database state is changed by phone scans.

## Verification

Run from the repository root (isolated test configurations contain no deployed
credentials):

```powershell
dotnet test src/Tests/POS.Api.Tests/POS.Api.Tests.csproj -m:1 /nr:false --filter FullyQualifiedName~PosScanner --verbosity quiet -clp:ErrorsOnly
dotnet test src/Tests/POS.IntegrationTests/POS.IntegrationTests.csproj -m:1 /nr:false --filter FullyQualifiedName~PosScanner --verbosity quiet -clp:ErrorsOnly
dotnet test src/Tests/Portal.E2ETests/Portal.E2ETests.csproj -m:1 /nr:false --filter FullyQualifiedName~PosScanner --verbosity quiet -clp:ErrorsOnly
```

Coverage includes unauthorized tenant/actor/session/capability, trusted tenant
delegation, challenge consumption/expiry, revocation, lease/hard expiry, replay,
bounded queues, payment pause, repeated-product sequences, stale callback lifetime,
concurrent mobile entry, real generated wrappers/handlers through loopback Bolt,
local WASM decoding and deterministic video, denied camera permission, and compact
pairing dialog/mobile layouts at 390px and 1366px in isolated Playwright Chromium.
The positive register-create EF query has not been database integration-tested.
The browser fixtures use synthetic auth/mock wrappers, not a deployed login flow.

No physical phone camera, iOS Safari/Android browser, trusted deployment TLS,
independent deployed login/redirect, or device installation has been verified.
Those are rollout acceptance checks after the parent configures authorized HTTPS.
No deployment, Tailscale change, sale/payment operation or user Chrome was used.
