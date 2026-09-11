# Yap

A standalone .NET 10 Blazor Interactive Server chat app using XFramework's existing Communications chat client, IdentityServer wrapper, Storage wrapper, and Bolt transport. The supplied Yap design is adapted to fill the browser window on desktop and mobile; device frames and preview controls are removed.

## Included

- Workspace login, server-side token refresh, and sign-out.
- Conversation list, search, unread filter, pagination, and direct/group creation.
- Message history, sending, reply threads, editing, deletion with confirmation, pinning, saving, and muting.
- Message search across conversations or within the current conversation, with paged results that open the matching message.
- Tenant-provisioned reaction choices, grouped reaction badges, and add/remove toggles with current-user ownership.
- File uploads up to 20 MB, attachment linking, and short-lived download links through the message actions menu. Backend attachment policies may impose stricter limits.
- Durable user events, typing updates, periodic history reconciliation, and read acknowledgement after the active conversation renders.
- Responsive inbox/conversation layout and persistent light/dark preference.

Voice/video calls and account signup are outside this first build. There are no simulated conversations or automatic replies in the production app.

## Connect to XFramework

Use an existing XFramework environment with IdentityServer, Communications, Storage, and Bolt Hub running. This app needs no database connection or new database. Modules retain their existing single-database, schema-per-module ownership.

1. Register a dedicated service client named `XFramework.Yap` in the environment's `ServiceIdentity:Clients` configuration using its normal credential provisioning process. Give it a new generation ID and secret; do not reuse another application's credentials.
2. Allow the audiences `XFramework.Bolt.Hub`, `XFramework.IdentityServer`, `XFramework.Communications`, and `XFramework.Storage`, and the explicit scopes from this app's `ServiceIdentity:DefaultScopes`: `bolt.service`, `communications.chat`, `identity.session.validate`, `datacontext.query`, `storage.read`, `storage.write`. The Hub must admit this registered service identity.
3. Choose an existing tenant, enabled account, and permitted role. People search and member names use IdentityServer's authorized, tenant-scoped credential read endpoint; the account also needs the corresponding `identity.credentials` read permission/feature. Communications membership and policy checks still apply to every chat operation.
4. Enable Communications Chat View/Create/Update/Delete for the member role. On connection, Yap calls the Communications-owned `EnsureChatDefaultsAsync` workflow to provision or discover fixed tenant chat, reaction, and delivery defaults. The returned thread type is used for groups; no type IDs need to be hardcoded in the app. This requires the chat-default SDK/API deployment.
5. Deploy the actor-owned chat attachment APIs and configure a working Storage provider and Communications attachment policy. Upload creation and download authorization go through the Communications chat session; dedicated Storage chat operations handle parts, completion, and abort. Ordinary members need Storage View/Create, not Manage. For S3, configure a browser-reachable signing endpoint before generating private download URLs. See the [attachment workflow](../../../docs/solutions/architecture-patterns/communications-chat-attachment-ownership.md).
6. Deploy the matching Bolt Hub before using this client's actor-isolated transient subscriptions. See [shared-client subscription compatibility](../../../docs/solutions/architecture-patterns/bolt-transient-actor-subscriptions.md).

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

The browser talks to this Blazor host. The host uses `ICommunicationsChatClient` for chat operations and subscriptions, `IIdentityServerServiceWrapper` for authentication, and `IStorageServiceWrapper` for files. `ChatDirectory` only reads authorized credential data through `RemoteDataContext`. No business mutation uses generic data-context writes.

The authentication cookie contains an opaque session key and display claims. Access and refresh tokens stay in server memory. Session refresh is serialized per login; sign-out revokes the local session before attempting upstream logout. Login and logout require antiforgery tokens. HTML renders message text as text, and attachment links require successful Communications membership validation first.

This first host is intended for one instance. Sessions expire after eight hours and server restarts require sign-in again. Horizontal deployment needs shared session storage and appropriate Blazor connection affinity. Message send RPCs are not automatically retried because a timeout may occur after commit. The composer preserves an uncertain draft and asks the user to refresh before retrying. Attachment retries check an already-committed link. A completed upload abandoned before linking can remain in Storage and should follow the environment's file retention policy.

## Verify

```powershell
dotnet build src/Presentation/XFramework.Yap/XFramework.Yap.csproj -m:1 /nr:false
dotnet test src/Tests/Yap.Tests/Yap.Tests.csproj -m:1 /nr:false
```

The tests cover authentication/antiforgery through the actual HTTP host, token isolation and refresh, chat event/cancellation behavior, uncertain sends, message order/read acknowledgements, search scope and older-history navigation, reply-thread refresh, group typing and rapid restart, reaction ownership, and attachment part hashes, negotiated chunks, verification status, failed-upload cleanup, and authorized download-link IDs.

For UI review without a live backend, the **test project only** provides an isolated in-memory fixture:

```powershell
dotnet run --project src/Tests/Yap.Tests/Yap.Tests.csproj -- --serve
```

Open `http://127.0.0.1:5189` and sign in with `fixture` / `fixture`. This test host removes Bolt background connections and supplies fixture wrappers. Its local `POST /test/incoming` endpoint injects an incoming message for realtime UI checks. None of this fixture data, authentication, or endpoint is registered by the production host. Stop the fixture before rebuilding the test project on Windows to avoid locked output files.

UI assets were adapted from the user-supplied `Yap-source.zip`. Live backend integration still requires verification against the configured environment; fixture results do not establish that the environment's permissions, service registration, storage provider, or network routes are ready.
