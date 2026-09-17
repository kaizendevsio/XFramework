# Yap

A standalone .NET 10 Blazor WebAssembly PWA using XFramework's existing Communications chat client, IdentityServer wrapper, Storage wrapper, and Bolt transport. The supplied Yap design is adapted to fill the browser window on desktop and mobile; device frames and preview controls are removed.

## Included

- Workspace login, server-side token refresh, and sign-out.
- Conversation list, search, unread filter, pagination, and direct/group creation.
- Message history, sending, reply threads, editing, deletion with confirmation, pinning, saving, and muting.
- Message search across conversations or within the current conversation, with paged results that open the matching message.
- Tenant-provisioned reaction choices, grouped reaction badges, and add/remove toggles with current-user ownership.
- File uploads of any type up to 4 GB, attachment linking, and short-lived download links through the message actions menu. Nothing is re-encoded or compressed; the chosen file is stored byte for byte. Attachments up to 64 MB are copied onto the device so they can be queued offline; larger files stream to the bucket in 8 MB parts while you compose and therefore need a connection. Backend attachment policies may impose stricter limits.
- Durable user event refresh hints, periodic history reconciliation, and read acknowledgement for the active conversation.
- The original prototype's liquid glass, theme and glass settings, animated navigation, message actions, swipe-to-reply, and full-page reply threads, filling the actual browser viewport without a device frame.
- Offline conversation history, reply history, drafts, queued messages/uploads, and opened attachments in a browser-local SQLite/OPFS store.

The sign-in page links to account registration. New accounts receive the configured workspace's regular member role and can then sign in. Voice/video calls remain outside this build. There are no simulated conversations or automatic replies in the production app.

## Connect to XFramework

The xeon-dev deployment hosts Yap at `https://xeon-dev.tailed40e.ts.net:5188` for tailnet users. The normal workflow builds its image, checks Bolt readiness, and configures its own Tailscale Serve listener. Backend HTTP binds only to `127.0.0.1:5188`; other Serve routes are preserved. The protected deployment environment supplies the dedicated service credentials plus `YAP_TENANT_ID` and `YAP_ROLE_ID`. The dev provisioning script initializes missing workspace values to the provisioned Yap test workspace and preserves explicit overrides. Data Protection keys persist in the `yap-keydata` volume. Login sessions are held in the shared cache named by `Yap__SessionCacheConnection` (Redis in compose) as Data Protection ciphertext, so a restart or rollout no longer signs everyone out. Without that setting the host falls back to an in-process store, which is fine for local runs and tests but loses every session on restart.

Use an existing XFramework environment with IdentityServer, Communications, Storage, and Bolt Hub running. The host needs no database connection or new module database; the browser owns a private local SQLite cache. Modules retain their existing single-database, schema-per-module ownership.

1. Register a dedicated service client named `XFramework.Yap` in the environment's `ServiceIdentity:Clients` configuration using its normal credential provisioning process. Give it a new generation ID and secret; do not reuse another application's credentials.
2. Allow the audiences `XFramework.Bolt.Hub`, `XFramework.IdentityServer`, `XFramework.Communications`, `XFramework.Storage`, and `XFramework.Notifications`, and the explicit scopes from this app's `ServiceIdentity:DefaultScopes`: `bolt.service`, `communications.chat`, `identity.session.validate`, `identity.profile`, `datacontext.query`, `storage.read`, `storage.write`, `identity.register`, `tenant.target`, `notifications.send`. The Hub must admit this registered service identity. `notifications.send` is used for one thing only: ringing a closed device for an incoming call, which never passes through the Communications outbox. Omit it and everything except call push still works.
3. Choose an existing tenant, enabled account, and permitted role. People search and member names use IdentityServer's authorized, tenant-scoped credential read endpoint; the account also needs the corresponding `identity.credentials` read permission/feature. Communications membership and policy checks still apply to every chat operation.
4. Enable Communications Chat View/Create/Update/Delete for the member role. On connection, Yap calls the Communications-owned `EnsureChatDefaultsAsync` workflow to provision or discover fixed tenant chat, reaction, and delivery defaults. The returned thread type is used for groups; no type IDs need to be hardcoded in the app. This requires the chat-default SDK/API deployment.
5. Deploy the actor-owned chat attachment APIs and configure a working Storage provider and Communications attachment policy. Upload creation and download authorization go through the Communications chat session; dedicated Storage chat operations handle parts, completion, and abort. Ordinary members need Storage View/Create, not Manage. For S3, configure a browser-reachable signing endpoint before generating private download URLs. See the [attachment workflow](../../../docs/solutions/architecture-patterns/communications-chat-attachment-ownership.md).
6. Deploy the matching Bolt Hub before using this client's actor-isolated transient subscriptions. See [shared-client subscription compatibility](../../../docs/solutions/architecture-patterns/bolt-transient-actor-subscriptions.md).
7. Enable registration in IdentityServer with `SelfRegistration:Clients:0:ClientId=XFramework.Yap`, `TenantId`, and `RoleId` under the same section. Use the same workspace/role configured in Yap. The tenant and role group must be active; the role must have `RoleLevel=0`. Missing or invalid configuration disables registration. Compose supplies these settings from `YAP_TENANT_ID`/`YAP_ROLE_ID`. The registration handler only admits the authenticated Yap service with `identity.register` and `tenant.target`; it checks the effective tenant against this policy before creating any records. It does not expose administrator credential-creation permissions.

### Push subscription permissions

Enable the `notifications.push` (Push Subscriptions) tenant feature and grant the Yap member role **View, Create, and Delete** for that feature through Portal's tenant and role-permission settings. Preserve the existing permissions and the tenant's default-deny policy. These grants cover configuration, registering the signed-in user's device, and removing that user's subscription; they do not grant module-wide Notifications permissions or permission to send push notifications.

The parent `notifications` feature must remain enabled for notification delivery. `/api/notifications/push/send` retains the parent gate and its trusted-service caller/scope restrictions. VAPID keys and service scopes alone are insufficient for the actor-owned subscription routes: missing member permissions produce a Notifications feature-gate 403, which `/api/chat/push/config` currently presents as `enabled:false`. Restarting containers does not provision role permissions. Existing installations must enable the new feature and apply the role grants after deploying this permission boundary; new accounts inherit the configured member role.

From the repository root, replace the placeholders below with your environment values:

```powershell
$yapProject = 'src/Presentation/XFramework.Yap/XFramework.Yap.csproj'
dotnet user-secrets set --project $yapProject 'Yap:TenantId' '<tenant-guid>'
dotnet user-secrets set --project $yapProject 'Yap:RoleId' '<role-guid>'
dotnet user-secrets set --project $yapProject 'BoltConfiguration:ServerUrls:0' 'wss://<bolt-host>/bolt/ws'
dotnet user-secrets set --project $yapProject 'ServiceIdentity:Authority' 'https://<identity-host>'
dotnet user-secrets set --project $yapProject 'ServiceIdentity:GenerationId' '<provisioned-generation-id>'
dotnet user-secrets set --project $yapProject 'ServiceIdentity:ClientSecret' '<provisioned-secret>'
dotnet run --project $yapProject
```

The development launch profile opens at `http://localhost:5188`. HTTPS/WSS service connections must have valid trusted certificates. The checked-in values deliberately contain no credentials and cannot connect to a live environment without setup. Missing service credentials fail startup validation. Production settings belong in the deployment secret store, using environment names such as `ServiceIdentity__ClientSecret`; set `AllowedHosts` for the actual hostname and serve HTTPS with WebSocket support.

## Architecture and operational limits

The browser runs the complete UI in WebAssembly and calls the same-origin `/api` host. There is no Blazor Server circuit. The host uses `ICommunicationsChatClient` for chat operations and subscriptions, `IIdentityServerServiceWrapper` for authentication, and `IStorageServiceWrapper` for files. `ChatDirectory` only reads authorized credential data through `RemoteDataContext`. No business mutation uses generic data-context writes.

The authentication cookie contains an opaque session key and display claims. Access and refresh tokens never reach the browser; they live in the session store under that key, sealed with Data Protection so the shared cache only ever holds ciphertext. Session refresh is serialized per login; sign-out revokes the local session before attempting upstream logout. Login, registration, and logout require antiforgery tokens. Registration saves the identity, BCrypt credential, and configured role in one database transaction, enforces username uniqueness and the existing password byte limit, and applies a distributed limit of ten registrations per application per minute. Registration creates no authentication session; users sign in after confirmation. HTML renders message text as text, and attachment links require successful Communications membership validation first.

The sign-in rolls rather than running down a fixed timer, because an installed messenger must not sign you out while you are using it. Any authenticated request pushes the idle deadline seven days out again; a single sign-in still ends twenty-eight days after it started no matter how much it is used. The cookie and the server-side session entry carry the same deadline and are renewed together from `OnValidatePrincipal`, and the store entry's TTL tracks that deadline so Redis cannot evict a session the app still honours. Both numbers are bounded by IdentityServer: its refresh token lives fourteen days and slides on each rotation, and Yap signs in with `RememberMe` so the upstream session carries the thirty-day cap instead of the twenty-four-hour default that no refresh extends. Sign-out still revokes locally and upstream immediately, and a longer session changes nothing about message encryption, which stays gated per device.

On narrow screens or coarse-pointer devices, editable controls use 16px text to avoid input-focus zoom. The viewport, touch-action rules on nested scrollers, and Safari gesture/multi-touch handlers disable browser pinch/double-tap zoom while retaining ordinary scrolling and taps. Mobile styles cover login, registration, conversation search, message composers, and dialog controls. Desktop input sizing is retained. OS-level magnification and browser accessibility overrides remain outside the app's control.

## Install on a phone

Yap provides a web app manifest with a stable identity, standalone display, Android icons, and an Apple touch icon. Serve it over HTTPS (localhost is allowed for development). On iPhone/iPad, open Safari's Share menu and choose **Add to Home Screen**, leaving **Open as Web App** enabled if shown. On Android Chrome, choose **Install app** / **Add to Home screen** from the browser menu. Installation help is also available on sign-in, registration, and Settings; it is hidden when running standalone.

## Push notifications

Yap uses real Web Push (RFC 8030 delivery, RFC 8291 payload encryption over the RFC 8188 `aes128gcm` content coding, RFC 8292 VAPID). A push reaches an installed PWA that is backgrounded or fully closed, which matters here because `chat-socket.mjs` deliberately tears the live socket down whenever the page is hidden: without push, a closed device learns nothing until it is reopened.

Subscriptions are owned by the Notifications module (`Notifications.NotificationPushSubscription`), not by this host, which keeps no database. The browser endpoint, its `p256dh` public key, and its `auth` secret are stored per device per credential, unique on `(TenantId, sha256(endpoint))`. A push service replying 404 or 410 deletes the row immediately rather than retrying a dead endpoint forever.

**What a notification contains.** Nothing readable. Message bodies are end-to-end encrypted and the server cannot decrypt them, so the payload is routing identifiers only - a version, a kind (`message` or `call`), the conversation ID, the inbox item ID, and an opaque reference. The service worker renders a fixed string ("New message" / "Incoming call") and the app fills in the real content after it opens and decrypts locally. No sender name, preview, or conversation title is sent to Apple, Google or Mozilla, and none is written into the delivery-job row either.

**One banner per message.** A message notification is tagged with the inbox item ID (`yap-msg-<id>`), so three messages stack as three banners instead of one replacing the next; a redelivery of the same push carries the same ID and replaces its own banner. Calls still collapse on the call reference. On a module worker the handler waits up to 2 s for a local decrypt and then shows a single notification - the decrypted one if it succeeded, the generic one otherwise. It is deliberately one `showNotification` call on every path: iOS does not honour tag replacement, so showing a generic banner first and replacing it left two banners per message.

**Configure VAPID.** Generate one P-256 key pair per deployment (for example `npx web-push generate-vapid-keys`) and supply it to the **Notifications** service, never to this host:

| Setting | Environment variable | Notes |
| --- | --- | --- |
| `Notifications:Push:Vapid:PublicKey` | `Notifications__Push__Vapid__PublicKey` | Base64url uncompressed P-256 point, 65 bytes. Served to browsers; public by design. |
| `Notifications:Push:Vapid:PrivateKey` | `Notifications__Push__Vapid__PrivateKey` | Base64url 32-byte scalar. Secret. Use the deployment secret store; never commit it. |
| `Notifications:Push:Vapid:Subject` | `Notifications__Push__Vapid__Subject` | `mailto:` or `https:` contact required by RFC 8292. |
| `Notifications:Push:TimeToLiveSeconds` | `Notifications__Push__TimeToLiveSeconds` | Optional, default 3600. Call pushes override this with the invite's own deadline. |

A tenant can override the deployment keys with a `NotificationProviderSetting` row for channel `Push` whose `SettingsJson` is `{"publicKey":"...","privateKey":"...","subject":"..."}`. **Until keys are configured, push disables itself**: the configuration endpoint reports `enabled: false`, Settings explains that the server has no push configured, no subscription can be created, and no delivery job is ever queued. One warning is logged per process, not per attempt.

**iOS.** Apple exposes the Push API only to a PWA installed to the Home Screen, on iOS/iPadOS 16.4 or newer. Safari tabs get nothing. The requirements this app relies on are: a linked `manifest.webmanifest` with `"display": "standalone"` and a `"scope"` covering the app, an HTTPS origin, a registered service worker (`updates.js` registers `service-worker.js` at the root scope), `Notification.requestPermission()` called from a real user gesture, and `PushManager.subscribe({ userVisibleOnly: true })`. The Settings toggle calls into `yap.push.enable` with nothing awaited first, so the click's transient activation is still valid when the prompt appears. iOS also requires that every push display a notification - a silent data-only push is not possible - which is why the payload has to be enough to render a banner on its own.

**Denied permission is terminal in-page.** Once someone blocks notifications, no web API can re-prompt; it has to be changed in browser or OS settings. Settings says exactly that instead of showing a toggle that does nothing.

**Latency.** A message push is queued as a Notifications delivery job by the existing Communications fan-out and dispatched on a signal rather than on the poll timer, so it normally leaves the server within a second of the message being written; the poll interval remains the recovery path. A call push does not use the job pipeline at all - `YapCallGateway` sends it directly over Bolt the moment the invite is created, with `Urgency: high` and a TTL matched to the 60-second invite, because a ring that arrives after the invite expires is worse than none. The TTL is recomputed from the invite's remaining lifetime immediately before each send (`YapCallGateway.RemainingSeconds`, floored to the second), so scoping, transport, and a slow push service eat into it instead of extending delivery past the deadline the gateway enforces. Nothing here promises immediate delivery: a push service may batch, defer, or drop a push, and a device that is off or offline gets nothing until it returns. That delivery loop owns no tenant of its own, so the **Notifications** service identity needs `datacontext.query.all-tenants` (to find which tenants have queued jobs) alongside `tenant.target` (to re-enter per tenant). Without both, every queued message push stays in the table and only call push - which never touches the job pipeline - reaches a device. A call push does not use the job pipeline at all - `YapCallGateway` sends it directly over Bolt the moment the invite is created, with `Urgency: high` and a TTL matched to the 60-second invite, because a ring that arrives after the invite expires is worse than none.

**Incoming-call notifications.** The call payload carries the invite's absolute deadline alongside the existing routing identifiers - still no caller name, message content, keys, or tokens. A ringing banner asks for a short double buzz (`vibrate: [200, 100, 200]`) and offers two buttons:

- **Open call** focuses an open Yap window, or opens Yap when none is running, and hands it the conversation deep link. The worker holds no credentials and checks nothing itself: the app re-enters the ordinary authenticated call flow, which replays only invites that are still live and answers `404`/`410` for anything ended or expired. Tapping the banner body does exactly the same thing. Neither path joins a call or touches the microphone - accepting stays an explicit action in the app.
- **Dismiss** closes the banner on that device and nothing else. No request is sent, so the call keeps ringing on the person's other devices and no participant is told anything; declining for real remains an authenticated action in the app.

A push delivered after the deadline renders "Missed call" instead of an invitation: silent, no buttons, no `requireInteraction`, and tagged with the same call reference so it replaces a stale ringing banner. It is deliberately still a visible notification - a `userVisibleOnly` subscription whose handler shows nothing can cost the whole origin its push permission, and a missed call is worth knowing about.

**What browsers ignore.** `vibrate` and `actions` are advisory. iOS ignores both: Home Screen PWAs render the banner without buttons and without vibration, so the body tap is the only control there, which is why it behaves like Open call. Firefox and Safari on the desktop also render no action buttons. There are no custom background ringtones and no forced full-screen call UI: a web push cannot supply either, so the system notification sound is whatever the OS gives the browser, and the in-page ring only starts once Yap is open and visible.

**Still needs a physical device.** Vibration, banner sound, action-button rendering, and lock-screen behaviour cannot be exercised by the worker test suite. Check on a real Android Chrome install and a real Home Screen iOS PWA: a ring while the app is closed, a ring while it is backgrounded, Dismiss on one device leaving another still ringing, Open call from a cold start, and a call answered or cancelled elsewhere arriving late enough to render the missed-call banner.

## Offline storage and synchronization

`XFramework.Yap.Client` owns EF Core entities for cached conversations/messages, drafts, and a transactional outbox. `SqliteWasmBlazor` 0.9.3-pre supplies the native SQLite WASM bridge and OPFS persistence. This is a community prerelease provider, not Microsoft's standard browser SQLite provider. The published build has been tested for transaction rollback, persistence after reload, offline startup, and recovery after a lost send response. Use a current browser with OPFS support (Safari 16.4+); this provider currently allows one active Yap database tab per origin. A second tab displays a recoverable storage message. Private browsing, device storage pressure, and clearing site data can remove local data. Settings can request persistent storage; the browser decides whether to grant it.

The service worker caches versioned application assets and the public SPA shell, and handles `push` and `notificationclick`. It never caches API or authentication responses. A tap focuses an already-open window and hands it the deep link rather than opening a second copy of the app. The development and published workers carry identical push handlers, and `src/Tests/Yap.Tests/service-worker.test.mjs` runs the same push suite against both files so they cannot drift. Private chat data is stored separately, scoped by tenant and credential. Access tokens, refresh tokens, and service credentials stay on the server. Sign-out clears the local cache, drafts, queued sends, and files; an offline sign-out records a durable logout intent before clearing private state and revokes the cookie session after reconnecting. Switching cookie identity cannot send the previous account's outbox.

An outgoing message is saved in a SQLite transaction before the composer clears. A stable `ClientMessageId` lets Communications recognize a matching retry after a timeout without creating a second message or outbox event; authorization and membership are checked before returning that receipt. Permanent API failures pause that queued item for review/retry. Upload bytes live in OPFS; SQLite stores the message, upload receipt, and link progress. A completed upload receipt survives reload so reconnect can finish attachment linking. An upload that completes on the server but loses its response can leave an unlinked Storage object; retention policies still apply. Opened attachments are cached for offline download. Files are downloaded rather than executed inside the app's origin.

Synchronization runs while Yap is open, when connectivity returns, and after authenticated refresh hints. A closed or backgrounded app receives a Web Push wake-up instead (see below); it does not sync in the background beyond that. Offline use covers data already loaded on that device; registration, new conversations, reactions, edits, deletions, and previously unopened files require a connection. Replies use the existing Communications parent-message relationship, so they also remain visible in conversation history.

Application updates prompt before activating a new service worker and wait for pending local database writes. This is the first local schema; future schema changes must use explicit non-destructive migrations rather than deleting the database. Sessions live in the shared store described above, so they survive a host restart or rollout.

The same-origin SSE endpoint sends only a refresh hint. Actual chat data is reread through authorized SDK operations; the Communications Bolt/MemoryPack topic contracts remain unchanged. The BFF checks an account scope header (or the account query on EventSource), validates antiforgery on writes, and serves attachment bytes through the authorized chat download workflow.

## Verify

```powershell
dotnet workload install wasm-tools
dotnet publish src/Presentation/XFramework.Yap/XFramework.Yap.csproj -c Release -m:1 /nr:false
dotnet test src/Tests/Yap.Tests/Yap.Tests.csproj -m:1 /nr:false
dotnet test src/Tests/Yap.Client.Tests/Yap.Client.Tests.csproj -m:1 /nr:false
```

The client tests cover real SQLite transactions, cache reconciliation, outbox retry identity, and account isolation. Host tests cover the browser API account/antiforgery boundary plus authentication through the actual HTTP host, token isolation and refresh, chat event/cancellation behavior, uncertain sends, message order/read acknowledgements, search scope and older-history navigation, reply-thread refresh, group typing and rapid restart, reaction ownership, and attachment part hashes, negotiated chunks, verification status, failed-upload cleanup, and authorized download-link IDs.

For UI review without a live backend, the **test project only** provides an isolated in-memory fixture:

```powershell
dotnet run --project src/Tests/Yap.Tests/Yap.Tests.csproj -- --serve
```

Open `http://127.0.0.1:5189` and sign in with `fixture` / `fixture`. This test host removes Bolt background connections and supplies fixture wrappers. Its local `POST /test/incoming` endpoint injects an incoming message for realtime UI checks. None of this fixture data, authentication, or endpoint is registered by the production host. Stop the fixture before rebuilding the test project on Windows to avoid locked output files.

UI assets were adapted from the user-supplied `Yap-source.zip`. Verified live scenarios, deployed revisions, attachment integrity results, and recovery checks are recorded in [the live test report](LIVE-TEST-REPORT.md). Configure and validate the service identity, permissions, provider, and network routes when connecting a different environment.
