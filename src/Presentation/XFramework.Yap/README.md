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
2. Allow the audiences `XFramework.Bolt.Hub`, `XFramework.IdentityServer`, `XFramework.Communications`, and `XFramework.Storage`, and the explicit scopes from this app's `ServiceIdentity:DefaultScopes`: `bolt.service`, `communications.chat`, `identity.session.validate`, `datacontext.query`, `storage.read`, `storage.write`, `identity.register`, `tenant.target`. The Hub must admit this registered service identity.
3. Choose an existing tenant, enabled account, and permitted role. People search and member names use IdentityServer's authorized, tenant-scoped credential read endpoint; the account also needs the corresponding `identity.credentials` read permission/feature. Communications membership and policy checks still apply to every chat operation.
4. Enable Communications Chat View/Create/Update/Delete for the member role. On connection, Yap calls the Communications-owned `EnsureChatDefaultsAsync` workflow to provision or discover fixed tenant chat, reaction, and delivery defaults. The returned thread type is used for groups; no type IDs need to be hardcoded in the app. This requires the chat-default SDK/API deployment.
5. Deploy the actor-owned chat attachment APIs and configure a working Storage provider and Communications attachment policy. Upload creation and download authorization go through the Communications chat session; dedicated Storage chat operations handle parts, completion, and abort. Ordinary members need Storage View/Create, not Manage. For S3, configure a browser-reachable signing endpoint before generating private download URLs. See the [attachment workflow](../../../docs/solutions/architecture-patterns/communications-chat-attachment-ownership.md).
6. Deploy the matching Bolt Hub before using this client's actor-isolated transient subscriptions. See [shared-client subscription compatibility](../../../docs/solutions/architecture-patterns/bolt-transient-actor-subscriptions.md).
7. Enable registration in IdentityServer with `SelfRegistration:Clients:0:ClientId=XFramework.Yap`, `TenantId`, and `RoleId` under the same section. Use the same workspace/role configured in Yap. The tenant and role group must be active; the role must have `RoleLevel=0`. Missing or invalid configuration disables registration. Compose supplies these settings from `YAP_TENANT_ID`/`YAP_ROLE_ID`. The registration handler only admits the authenticated Yap service with `identity.register` and `tenant.target`; it checks the effective tenant against this policy before creating any records. It does not expose administrator credential-creation permissions.

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

On narrow screens or coarse-pointer devices, editable controls use 16px text to avoid input-focus zoom. The viewport, touch-action rules on nested scrollers, and Safari gesture/multi-touch handlers disable browser pinch/double-tap zoom while retaining ordinary scrolling and taps. Mobile styles cover login, registration, conversation search, message composers, and dialog controls. Desktop input sizing is retained. OS-level magnification and browser accessibility overrides remain outside the app's control.

## Install on a phone

Yap provides a web app manifest with a stable identity, standalone display, Android icons, and an Apple touch icon. Serve it over HTTPS (localhost is allowed for development). On iPhone/iPad, open Safari's Share menu and choose **Add to Home Screen**, leaving **Open as Web App** enabled if shown. On Android Chrome, choose **Install app** / **Add to Home screen** from the browser menu. Installation help is also available on sign-in, registration, and Settings; it is hidden when running standalone.

## Offline storage and synchronization

`XFramework.Yap.Client` owns EF Core entities for cached conversations/messages, drafts, and a transactional outbox. `SqliteWasmBlazor` 0.9.3-pre supplies the native SQLite WASM bridge and OPFS persistence. This is a community prerelease provider, not Microsoft's standard browser SQLite provider. The published build has been tested for transaction rollback, persistence after reload, offline startup, and recovery after a lost send response. Use a current browser with OPFS support (Safari 16.4+); this provider currently allows one active Yap database tab per origin. A second tab displays a recoverable storage message. Private browsing, device storage pressure, and clearing site data can remove local data. Settings can request persistent storage; the browser decides whether to grant it.

The service worker caches versioned application assets and the public SPA shell. It never caches API or authentication responses. Private chat data is stored separately, scoped by tenant and credential. Access tokens, refresh tokens, and service credentials stay on the server. Sign-out clears the local cache, drafts, queued sends, and files; an offline sign-out records a durable logout intent before clearing private state and revokes the cookie session after reconnecting. Switching cookie identity cannot send the previous account's outbox.

An outgoing message is saved in a SQLite transaction before the composer clears. A stable `ClientMessageId` lets Communications recognize a matching retry after a timeout without creating a second message or outbox event; authorization and membership are checked before returning that receipt. Permanent API failures pause that queued item for review/retry. Upload bytes live in OPFS; SQLite stores the message, upload receipt, and link progress. A completed upload receipt survives reload so reconnect can finish attachment linking. An upload that completes on the server but loses its response can leave an unlinked Storage object; retention policies still apply. Opened attachments are cached for offline download. Files are downloaded rather than executed inside the app's origin.

Synchronization runs while Yap is open, when connectivity returns, and after authenticated refresh hints. Closing/suspending the app does not guarantee background delivery. Offline use covers data already loaded on that device; registration, new conversations, reactions, edits, deletions, and previously unopened files require a connection. Replies use the existing Communications parent-message relationship, so they also remain visible in conversation history. No calls or background push are included.

Application updates prompt before activating a new service worker and wait for pending local database writes. This is the first local schema; future schema changes must use explicit non-destructive migrations rather than deleting the database. Sessions remain server-memory backed, expire after eight hours, and require sign-in after a host restart. Multiple host instances would need shared session storage.

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
