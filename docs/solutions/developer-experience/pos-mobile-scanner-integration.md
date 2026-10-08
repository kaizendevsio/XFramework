# POS Mobile Scanner Integration

## Parent Cashier Hook

The scanner is independently scoped to this component instance, the selected
register, trusted tenant, cashier actor, and desktop login session. Cashier mounts
the pairing component in its header and keeps it mounted across sale generations.

```razor
@using XFramework.Portal.Features.POS.Scanner

<CashierScannerPairing
    SaleGeneration="_scannerSaleGeneration"
    RegisterId="@(TryResolveSelectedRegisterId() ?? Guid.Empty)"
    Disabled="@IsMobileScannerPaused"
    ScanReceived="ReceiveMobileScan" />
```

`ScanReceived` receives only a product code. A normally completed callback
acknowledges acceptance or a deliberate catalog rejection; throwing disconnects
the scanner without automatic redelivery, so check the cart before reconnecting.
The hook checks lookup errors, search revision, tenant, register and sale generation
before acknowledging. A caught transport failure or superseded lookup must not be
acknowledged as success. Any replacement hook must retain these guards.

`Disabled` pauses delivery; it does not disconnect. Paused polls renew the desktop
lease without draining or acknowledging the queue. New phone sends receive 423
(desktop busy), retain one pending code locally, and require explicit retry.
The component also checks `Disabled` immediately before each callback. Codes already
queued before the modal opened remain pending until it closes. Completed checkout,
cart clearing/replacement, and register or tenant changes rotate the sale generation
and discard old-sale queued scans. Only register/tenant changes revoke pairing.
Opening payment invalidates in-flight catalog
lookups; ordinary product addition and closing payment retain the pairing.

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
Portal__ScannerPublicBaseUrl=https://xeon-dev.tailed40e.ts.net:5443
```

Equivalent appsettings:

```json
{ "Portal": { "ScannerPublicBaseUrl": "https://xeon-dev.tailed40e.ts.net:5443" } }
```

The configured value must be an absolute HTTPS root origin without userinfo,
query, fragment, or path prefix. If absent, an HTTPS desktop origin is used;
HTTP desktop fallback is denied with a meaningful configuration error.
Both the QR and Phone scanner link use the secure origin. The phone logs in at
that host independently; there is no transfer of desktop cookies or tokens.
Cookie domain, SameSite, session and refresh-token behavior remain unchanged.
Normal wrappers reuse existing cached service/actor tokens.

The normal dev Compose maps the non-secret `PORTAL_SCANNER_PUBLIC_BASE_URL` to
`Portal__ScannerPublicBaseUrl`, defaulting to the origin above. Portal retains its
original `${PORTAL_EXPOSE_PORT:-5000}:8080` publishing: desktop
`http://xeon-dev:5000` remains usable. The candidate pins only the non-secret scanner
origin, not `PORTAL_EXPOSE_PORT`. The HTTPS ingress proxies the existing
`http://127.0.0.1:5000` listener; the dev backend must still be reachable there.

Serve terminates TLS, so Portal must recognize the trusted forwarded HTTPS scheme
before HSTS, HTTPS redirection, antiforgery and authentication. Compose maps the
non-secret `PORTAL_TRUSTED_PROXY_IP` to `Portal__TrustedProxyIp`. Full deployments
derive this exact IPv4 gateway from the project's default Docker bridge metadata
after core startup and write it into the candidate env before recreating Portal;
no subnet is hardcoded or trusted. Manual Compose HTTPS setups must explicitly set
the current bridge gateway. Blank disables forwarded scheme handling. Invalid
addresses fail startup. Only `X-Forwarded-Proto` is enabled, with a one-hop limit,
the explicit proxy address and retained framework loopback trust defaults; client
IP and Host forwarding are not enabled. Do not enable the trust-all
`ASPNETCORE_FORWARDEDHEADERS_ENABLED` flag or clear known proxy/network restrictions.
The existing `SameAsRequest` cookie policy therefore emits Secure cookies through
trusted HTTPS ingress while direct HTTP and default HTTPS redirection behavior are
unchanged. Untrusted peers cannot change the scheme with a spoofed header.
The bridge gateway is a trust boundary: other host-local processes able to use
the published loopback listener are also trusted, not cryptographically identified
as Serve. Restrict host access accordingly. See [ASP.NET Core proxy guidance](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).

On a full authorized **Deploy Xeon Dev** release,
`scripts/configure-portal-scanner-dev-ingress.sh configure <ownership-marker>`
adds only **tailnet-only HTTPS 5443 -> http://127.0.0.1:5000** using targeted
Tailscale Serve. It refuses conflicting listeners, paths, foreground owners,
or Funnel exposure on 5443, reuses an exact existing private mapping without
mutating it, and verifies HTTPS readiness and the Portal browser runtime with
normal certificate validation. All other Serve routes and Funnel settings remain
untouched. The Yap-only deployment path does not configure scanner ingress.

The release records listener ownership before mutation. On deployment failure,
`rollback <ownership-marker>` removes only a matching listener newly created by
that candidate, before the previous Portal container is restored. Pre-existing
matching ingress is preserved; changed/conflicting ownership fails closed rather
than removing another route. No global Serve reset or whole-state restoration is
used. The previous release's candidate env restores its own proxy setting on
rollback; reused ingress is not removed. Implementation inspected only Docker
network gateway metadata remotely; no Serve configuration, deployment, credential
file, or protected env file was changed or read for this security fix.
Rollback to a release predating trusted-scheme handling can reintroduce non-Secure
HTTPS login cookies behind reused ingress. Do not use mobile login after rollback
until the restored release's Secure-cookie behavior has been verified or repaired.

After authorized rollout, connect the phone to the permitted tailnet, verify the
certificate and camera access at the configured origin, and sign in independently.
Verify the HTTPS login cookie has Secure and that direct desktop HTTP still works.
Tailnet ACL/grant access and HTTPS certificate eligibility must already permit
this host/port; the workflow does not change those policies. Tailscale Serve is
private to the tailnet, unlike Funnel ([Serve CLI reference](https://tailscale.com/docs/reference/tailscale-cli/serve)).
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
a sliding thirty-minute expiry renewed by authorized desktop heartbeats, and require
a desktop heartbeat within thirty seconds. Connected pairings do not display an
expiry/countdown and can last an entire shift. The two-minute one-use challenge
still expires before a phone claims it. Completing, clearing, or replacing a sale
discards pending old-sale codes without disconnecting the phone; tenant/register
changes, logout, explicit disconnect, or loss of the desktop lease end pairing.
Disconnect revokes immediately; failed revocation/logout loses its desktop lease
within thirty seconds. A component-specific random desktop key isolates tabs even
when their login session is shared. Phone keys are bound to the independent phone
login session. None of these keys is persisted in local storage or embedded in QR.

Each pairing queues at most 32 codes, with no per-shift scan-count cutoff; at most
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
delegation, challenge consumption/expiry, revocation, lease expiry, full-shift renewal, replay,
bounded queues, payment pause, repeated-product sequences, stale callback lifetime,
concurrent mobile entry, real generated wrappers/handlers through loopback Bolt,
local WASM decoding and deterministic video, denied camera permission, and compact
pairing dialog/mobile layouts at 390px and 1366px in isolated Playwright Chromium.
Five PostgreSQL integration cases exercise successful same-tenant and delegated
pairing creation/claim, and reject disabled, wrong-tenant or deleted registers.
Sixteen forwarded-scheme tests cover Secure cookies, untrusted spoofing and direct
HTTP behavior. Thirteen ingress/resolver tests cover ownership and rollback.
The browser fixtures use synthetic auth/mock wrappers, not a deployed login flow.
The focused POS Scanner Tests workflow runs API, PostgreSQL/Bolt, isolated camera,
cashier and forwarded-scheme tests for scanner-related pull requests.

No physical phone camera, iOS Safari/Android browser, trusted deployment TLS,
independent deployed login/redirect, or device installation has been verified.
Those are rollout acceptance checks after the parent configures authorized HTTPS.
No deployment, Tailscale change, sale/payment operation or user Chrome was used.
