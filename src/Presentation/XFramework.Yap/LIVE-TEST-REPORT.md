# Yap live setup and test report — 2026-09-11

**Status: direct and three-person group messaging, replies/threads, reactions, search, editing, pinning, saved messages, deletion, mute, unread/read transitions, persistence, and non-member rejection have live passing evidence. Shared typing subscriptions and regular-user attachment upload remain blocked pending their fixes and deployment.** Fixture tests remain separate from the live evidence below.

## Live checks after PR #441 deployment

Deployment `34583099498` completed successfully. These checks ran against that deployed server with the scope-corrected client, before importing PR #442's new transient subscription protocol. Local Yap was stopped once PR #442 deployment `34584776953` reached service rollout.

- **Restart/persistence:** all three accounts logged in again and loaded the existing group history and edited text. The earlier unpin had persisted for Bob and Carol. Carol's direct Love reaction and both direct replies survived app restarts. Removing her final Love reaction removed the chip for Bob too.
- **Mute:** Bob muted the group, reloaded, and saw Unmute conversation. Carol still saw Mute conversation. Bob unmuted; after another app restart/login, his action was Mute conversation again.
- **Timed delivery:** Bob sent `Yap E2E timed group delivery 0936`; browser DOM observers recorded arrival in Carol's and Dave's existing sessions at 3,995 ms and 3,979 ms respectively, without refresh. The server's outbox dispatcher polls every five seconds, so the earlier 2.5-second probe was too short to establish a delivery failure.
- **Delete UI repair:** opening Delete initially crashed the Blazor circuit because `BbAlertDialogAction` forwarded a string `OnClick` attribute to `BbDialogClose`. The confirm action now uses `BbButton` with a typed callback, and Cancel explicitly uses `AsChild="false"` to render a button. Live verification: the alert dialog opens, Keep message dismisses it without deletion, and Delete message removes Dave's disposable `Yap E2E delete probe 0932` from the other accounts. Searching that exact text returns zero results. All 26 Yap tests pass after the repair.
- **Group reactions:** all three members added Celebrate to Bob's timed-delivery message; Bob saw count 3. Dave removed his; Carol saw count 2 with her pressed state true, Dave saw count 2 with his false.
- **Group replies/threads:** Carol replied to that parent. Bob's thread showed one reply, then he sent a response from its thread composer. Carol's open thread updated to two replies with both correct authors. Bob subsequently deleted the parent while Carol's thread was open; her thread closed gracefully and `Yap E2E after deleted parent 0938` arrived afterward. The child messages remain in group history with their earlier-message reference.
- **Conversation filters and reuse:** Groups showed only the group. Starting a direct conversation with Carol again returned the same `ad1e82d5-6521-40b7-bb74-608929607586` URL and All still contained exactly two conversations, not a duplicate.
- **Search:** mixed-case `sTaBlE rEcOvErY` returned the group's matching message globally. Restricting to Carol's current direct chat returned zero. Restoring global scope and selecting the result navigated to the group and focused `message-3b99b416-4a17-492f-a0c4-b25d8f26eb28` with the matching text.
- **Unread/read:** with Carol viewing the group, Bob sent `Yap E2E unread check 0941` in their direct chat. Carol's direct row and Unread tab displayed 1. Unread filtered to that single direct conversation. Opening it while the document was visible and focused cleared the unread badge and left zero unread conversations.

Remaining acceptance work includes the deployed multi-actor typing fix, secure attachment upload/download (small and multipart), and final recovery/visual checks with the final client/server combination. PR #442 must finish deployment before importing its client because its transient routing protocol requires the matching Hub.

## Live chat checks with query-scope fix `7f2ba968`

Local Yap imported the tested SDK scope fix as `41f8fff9`; all 26 local tests passed. It used deployed server revision `94cc21b0` while PR #441 proceeded through normal deployment. The local app was stopped when core rollout began at approximately `09:22Z`, avoiding the known synthetic routing interference.

- **Member search:** Bob searched `cArOl` and received Carol, proving case-insensitive authorized lookup. Searching `yap` listed Alice, Carol, and Dave while excluding Bob.
- **Direct chat:** created `ad1e82d5-6521-40b7-bb74-608929607586` for Bob and Carol. The conversation appeared in both open sessions; Bob saw Carol's name and Carol saw Bob's. Each sent a message and the other received it without manual refresh.
- **Reactions:** Bob added Love to message `7dfe8f4f-f3eb-465e-9af2-27e2524c2ced`; Carol saw count 1. Carol added hers; both saw count 2 and their own pressed state. Bob removed his; count returned to 1, Bob's pressed state became false, and Carol's remained true.
- **Replies and threads:** Carol replied to Bob's first message. Bob opened its thread and saw the parent plus one reply. Bob sent from the thread composer; the panel showed two replies and Carol received the linked response in the main history.
- **Private access:** Dave opened the direct conversation URL. `GetThreadRequest` returned 403 at `09:12:21Z`; no private messages were rendered.
- **Group chat:** created `ded22f72-8d4c-42df-8816-96d1110b5758`, named `Yap E2E Group 0911`, with Bob, Carol, and Dave. It appeared for all three. Each sent a message; all three sessions showed all three messages without manual refresh.
- **Message search:** Bob's `Yap E2E` search returned all seven then-existing direct/group messages. Selecting Only this conversation returned exactly the three group messages. Selecting Dave's result focused DOM message `8183ee12-c0dd-4af1-ace0-9eb29bd16410` in the correct group.
- **Edit, pin, save:** Dave edited his group message; Bob and Carol saw the edit. Bob pinned it and Carol saw the pin. Bob saved it, reloaded the page, and still saw the edited text, pin, and Unsave action. Carol still saw Save message, confirming per-user saved state. Bob unsaved it and the action returned to Save message. Unpin was submitted; cross-user removal verification is pending.
- **Post-reload delivery:** Dave's additional group message ultimately reached Bob and Carol after Bob's reload, but a 2.5-second DOM observation did not see it arrive. No duplicate durable-subscription error appeared in the app log. Prompt delivery after reload must be retested outside deployment; subsequent connection/reconcile errors coincided with core rollout.

### Outstanding live blockers

1. **Typing is one-way with concurrent users.** Carol-to-Bob typing appeared and cleared; Bob-to-Carol did not. Logs at `09:06:05Z` show the second transient subscription rejected with `Already subscribed to topic`. `BoltClient` permits only one local subscriber per topic hash. The same error occurred twice when all three members opened the group. The fix task owns multi-subscriber support with separate actor authorization, independent cancellation, and reconnect tests. This was not a transport-token expiry failure.
2. **Attachments call an admin-only workflow.** Selecting `live-chat-attachment.txt` through Chrome succeeded, but `EnsureStorageUploadMetadataRequest` returned 403 at `09:16:22Z`, before creating an upload session or message. Generic Storage metadata management intentionally requires Manage. The fix task is implementing an appropriate chat upload path without granting admin permissions; it also found upload-session operations require an ownership/authorization review. The failed file was removed from the composer. No upload/download has passed live yet.

## Retest after PR #440 deployment

- Normal deployment `34580364797` completed successfully for develop `94cc21b02f697ef070384f11267e5e29ce6209e0`. Yap's local suite passes 26 tests against that merged code.
- `EnsureChatDefaults` now succeeds with regular Chat Create permission. A read-only database query verified tenant-owned Chat thread type `917b06a0-8a75-4155-8fa9-797ae54d109e`, Chat message type `d538734b-59ce-4e3a-8530-515d6bb60715`, and six reaction types: Love, Like, Laugh, Celebrate, Sad, Surprised.
- Bob's live logout now revokes the current upstream session. Session `166e08d5-b15e-4e44-8033-e1da35d51bfc` changed to `Inactive` at `08:53:27Z`; its previous session remained active. After logout, opening protected Settings redirects to login.
- Conversation-list loading initially returned 501 during the deployment smoke test, then succeeded after full rollout. The fix task found that the synthetic smoke runner registers the real Communications recipient name, temporarily sharing its route without business handlers. No app handler was patched for this transient failure; the deployment interference needs a separate pipeline correction.
- Bob and Carol now initialize the real chat workspace and load the empty conversation list. Carol's message search for `Yap E2E` returns zero results without error. Search with actual messages and scope restrictions is still pending.
- **Current blocker:** Bob's member search for `cArOl` fails before the query RPC. At `08:57:00Z`, service-token acquisition returned HTTP 403. `ServiceWrapperGenerator.cs` requests both `datacontext.query` and `tenant.target` for every normal query. Yap intentionally has only the former; its actor and requested tenant already match. Reported the exact source to the fix task for an actor-bound query scope correction, preserving cross-tenant restrictions. No broader grant was added.

## Live checks after PR #439

- Follow-up PR **#440** passed all CI checks and merged as `94cc21b02f697ef070384f11267e5e29ce6209e0`. Normal deployment `34580364797` is in progress. Yap has merged this develop revision and its 26 tests pass; the live failures below were observed before this new rollout and still require retesting afterward.
- Normal deployment run `34577532429` completed successfully for develop `f39f095744957fa6a2778af6a9776e933f7fc3a7`. Local Yap uses the merged SDK contracts.
- Imported the dedicated Yap service identity through the protected local handoff into server-only user secrets. Yap connected to Bolt through the loopback SSH tunnel at `08:18:41Z`; Bob signed in through Chrome using his regular tenant account.
- At `08:19:06Z`, `EnsureChatDefaultsRequest` returned **403**. The fix task correlated Communications logs and found that the trusted route gate infers **Manage** for `/api/communications/chat/defaults`, despite the endpoint declaring **Create**. The fix task owns the correction and deployment. No extra user permission was granted.
- Additional shared API blockers reported for fix/deploy: Yap is absent from the known service caller list for remote queries; collection `Contains` throws in the client expression parser; the active server query executor ignores OR markers and combines predicates with AND; people search needs an explicit case-insensitive operation.
- Added a test using the actual Yap directory and remote query serializer with a captured wrapper boundary. It reproduced `NotSupportedException: Static method call 'Contains' is not supported` before any RPC. After importing shared commit `64e07804`, it passes with 125 distinct member IDs in three queries. Membership resolution uses batches of at most 50, below the server's 64-value limit. People search now explicitly requests case-insensitive substring matching. Server deployment of these changes remains pending.
- Bob, Carol, and Dave each signed in and showed the correct distinct profile name on Settings. Bob's sign-out removed his local session and protected pages redirected to login; Carol remained signed in. However, upstream `LogoutRequest` returned **403** at `08:22:11Z`, so server session revocation has not passed. Reported this to the fix task and fixed Yap's response handling so a failed revocation is logged rather than silently ignored; local sign-out still completes.
- Imported peer DTO commit `29f463ba`; Yap resolves direct conversation titles from the other member, in a bounded directory batch for the loaded list. Previously it used the stored combined caller/peer title. Both caller perspectives are covered, and group names remain unchanged. Reply-thread composition now publishes typing updates through the existing conversation typing channel.
- A reply parent becoming unavailable now closes its open thread on the API's 404 response, allowing subsequent realtime refreshes to continue. A regression test covers deletion while the reply panel is open.
- Current local suite: **26 passed**, including the actual directory serialization regression, rejected-upstream-logout handling, both direct peer title perspectives, and deleted reply-parent handling. Live chat remains blocked; these tests are not live API passes.

## Progress after PR #438 deployment

### Follow-up application work and test accounts

- Created Bob, Carol, and Dave through the signed-in Chrome Portal, with the Yap tenant visibly selected. Each credential has the regular Yap Member role; a read-only database query verified every enabled assignment. Their passwords were generated for this test and are not recorded in this report. Alice's earlier generated password is no longer available in the browser tool session, so the three new accounts will be used for multi-user testing without resetting Alice.
- Bob: credential `7f8d38fe-bc8e-4902-9693-1a56ac7d2588`, username `yap.bob.20260911`, assignment `a6f4df0c-5ea2-42e6-975c-0c72b2b07f76`.
- Carol: credential `65780e17-4782-4b43-b3f8-cfd4e085fb70`, username `yap.carol.20260911`, assignment `a7d8b2e7-6c46-4d02-9cd1-782f51e08a9d`.
- Dave: credential `86a94d61-8d1e-4020-b785-96dcdd7c880b`, username `yap.dave.20260911`, assignment `eca27988-1dc3-411a-ab25-beb942d2c42b`.
- Brought Yap onto develop `fa0bc3b1`, then imported the fix task's SDK commits `1a1858b9` and `dcd02e9b`. API deployment is pending in PR #439. Yap now provisions/discovers fixed chat defaults through the module, uses `IsDirect` for group classification, displays batched reaction summaries, toggles the caller's reaction, searches messages through the SDK, and exposes paged reply threads.
- Fixed typing state per member, including expiry rendering and switching-conversation throttling.
- Local test result: **21 passed**. Fixture browser checks passed message search, focusing the matched message, reaction addition/removal, and sending a reply through the thread sheet. These remain fixture-only checks.
- Browser-tool limitation observed during these checks: native mouse commands produced no page pointer/click events, while text-input events and DOM activation of the visible buttons worked. Both Chrome extension automation and agent-browser reproduced it. The checks above used DOM activation of existing UI controls; native pointer interaction is not yet verified. Temporary click diagnostics were removed from source afterward.
- Opened loopback-only SSH forwards from local `18261` to IdentityServer `8261`, and `17000` to Bolt `7000`. Identity health through the tunnel returned HTTP 200. Local development settings use HTTP/WS inside this encrypted SSH tunnel; checked-in HTTPS/WSS defaults are unchanged. Dedicated Yap service credentials are still pending from the deploy task.

### Verified role provisioning

The fix task reported normal deployment `34571322907-1` of develop `fa0bc3b151f8f7d09c77a6eb1d47e1cb42575d9a`, then verified exactly one `Yap Members` role group through Chrome and a full reload. Its fix removed the page's unsupported caching decorator, rather than changing the host to the alternative registration suggested below. It reported all 84 non-browser Portal tests and PR CI passing.

This task continued in the signed-in Chrome session:

- Reused the single **Yap Members** group: entity ID `b24d70d1-c33c-4527-a78f-0c51aefdfd58`, system reference `2738f063-a8b5-4387-aafe-a048d5e8ddb6`.
- Created **Yap Member**, level `0`: role-type ID `633a467c-53a8-4c51-8f19-f655d5391f22`, system reference `e50f742f-3726-4f3f-90ce-b617dc6e01ae`. The new row is visible in both Reference Data and the tenant's Role Types page.
- Assigned it to Alice's credential `be0ec40d-c084-4608-8489-ab37a3111a83`. Chrome confirmed “Role assigned.” A read-only database query verified the enabled, non-deleted assignment `e68ead6e-a987-4e6f-8312-ff9cb0bddd81` in the correct tenant.
- Saved and reloaded role permissions: Communications Chat View/Create/Update/Delete; Identity Credentials View; Storage View/Create/Update. Other capabilities remain at the tenant default of Deny; no management capabilities were granted.
- Set local Yap user-secret `Yap:RoleId` to the actual role-type ID above. Authentication checks `IdentityRole.TypeId`, so this setting is not the individual assignment ID or the system-reference ID. `Yap:TenantId` already points to the Yap test tenant.

**New Portal display defect:** Alice's Assigned Roles section still says “No assigned roles” after successful assignment and Refresh, despite the persisted database row. The backend assignment succeeded; the list display/read path remains unverified. `UserDetail.LoadRoles` catches failures and converts them into an empty list; the underlying cause of this particular empty display has not been established. Do not create a duplicate assignment to compensate.

**Remaining Communications blocker:** a read-only query across the live `Communications.MessageThreadType` and `Communications.MessageReactionType` tables returned no enabled, non-deleted rows at all, not merely none for Yap. Direct chat requires an existing chat type; group chat requires a type ID; reactions require valid reaction types. Current source has no active reference-data provisioning UI/endpoint or seed workflow for these records. A supported owner-module provisioning path is needed before the requested live scenarios can run. No records were inserted directly into the database.

Yap's dedicated service-client registration remains pending. The historical failures below are retained for diagnosis and are superseded by the passing role provisioning results above.

## Retest after sign-in — new Portal failure

With **Yap E2E 20260911** visibly selected, opened **Role Groups → Add Role Group**, entered `Yap Members` and the original description, and clicked **Create** once. The dialog stayed open and displayed:

> Could not save reference data. Please try again.

The live Portal log at `06:21:56` recorded:

```text
Could not save reference data category role-type-groups
System.InvalidOperationException: Unable to resolve service for type
'XFramework.Integration.DataContext.Cache.IClientCacheService' while attempting to activate
'XFramework.Integration.DataContext.CachingDataContext'.
ReferenceData.CreateMutationContext(Guid tenantId): line 1178
ReferenceData.PersistAndClose[T](Action`1 applyChanges): line 1190
ReferenceData.SaveRoleTypeGroup(): line 1033
```

The save fails constructing its local context, before submitting the role mutation to IdentityServer. This result does not establish whether the original server-side 403 has been fixed.

Source inspection of the deployed fix's worktree (`codex/fix-reference-data-mutations`, commit `057fa7cd`, containing `9bb20b8b`) found a registration mismatch:

- `XFramework.Portal/Program.cs:137` calls the extension `builder.Services.AddRemoteDataContext()` from `XFramework.Integration.Extensions`. That helper registers only the uncached `IDataContext`.
- `ReferenceData.CreateMutationContext` explicitly constructs `CachingDataContext`, which additionally needs `IClientCacheService` and `DataContextOptions`.
- `ReferenceDataSaveTests` explicitly calls `RemoteDataContextExtensions.AddRemoteDataContext(services)` from the `DataContext` namespace. This registers those dependencies, so its service collection differs from the actual host.

Candidate correction in Portal startup: select the cache-enabled registration explicitly, then run a test with the host's actual registration path and retry the live form:

```csharp
XFramework.Integration.DataContext.RemoteDataContextExtensions
    .AddRemoteDataContext(builder.Services);
```

This correction has been identified but has not been applied or deployed in this task. The existing deployment and the other fix worktree were preserved. The follow-up automation remains paused.

## Deployment recheck

The fix was deployed locally under the existing Compose release directory, so GitHub workflow history and the release directory alone did not detect it. Both containers are healthy:

- IdentityServer: `xframework-local/identityserver:reference-data-20260911`, image `sha256:b3bf36cc40eb5b9b1ab644395fa01ebe9b882af12ba7e5b93d7d388604cc98a9`, started `2026-09-10T18:37:36Z`.
- Portal: `xframework-local/portal:reference-data-20260911`, image `sha256:44e998bdf0afb439f1b62baa04855970785d3518c4b66817fc1a7c74d676e9c9`, started `2026-09-10T18:37:58Z`.

The monitor incorrectly treated the unchanged release label as proof that deployment was still pending. Future checks must compare actual image IDs and container start times as well as workflow/release metadata.

Both existing Chrome Portal tabs now show `/login?returnUrl=%2Fadmin%2Freference-data` with empty username and password fields. The original tab is left ready for the user's Super Admin sign-in. Role creation must still be retried before marking the 403 fixed in the live test results. Findings below describe the original failure, not a retest of the patched images.

## Environment and records

- Used the existing signed-in Super Admin session in Chrome at `http://xeon-dev:5000`.
- Deployed release: `34500515787-1`, commit `720d798a5eca9ce5a668fb21b0a0137f5438c27f`.
- Created active tenant **Yap E2E 20260911**, ID `c4af50e9-325c-4b17-a4e9-ada54c9475b9`.
- Created **Alice Yap Test**, identity name `yap-alice-20260911`, identity ID `4774516e-cb78-45df-9c6b-6eea10fa582e`.
- Created credential **yap.alice.20260911**. No role is assigned; this is not a ready-to-use chat account. Passwords are excluded from this report.
- Soft-deleted the unused preliminary identity `b2145285-72a2-492f-8d3b-114601512d46` (`yap-e2e-alice-20260911`), which had no credential. Chrome confirmed “User deleted.”
- No backend deployment, service restart, direct database mutation, or authorization relaxation was performed.

## Confirmed live failure: role provisioning

Reproduction:

1. Open **Reference Data** while the active tenant is **Yap E2E 20260911**.
2. Select **Role Groups**, then **Add Role Group**.
3. Enter name `Yap Members` and description `Regular chat participants for Yap end-to-end tests`.
4. Click **Create**.

Observed result: `Error: DataContext change request failed with status 403 (Forbidden).`

Alice's **Assign Role** dialog has no role types to select. A read-only database transaction confirmed that the Yap tenant has **zero role groups and zero role types** after the failed request.

Source explanation: `ReferenceData.razor` calls `PersistAndClose`, which submits `IdentityRoleTypeGroup` through generic `IDataContext` mutation. Both `IdentityRoleTypeGroup` and `IdentityRoleType` expose read-only generated endpoints and are not allowlisted for generic remote writes. The existing authorization service supports assigning existing roles and configuring their permissions, but no role-definition creation workflow was found. The correct repair is an authorized IdentityServer-owned provisioning workflow and a Portal wrapper call, preserving the generic-write restriction.

Relevant source:

- `src/Presentation/XFramework.Portal.Features.Administration/Pages/Admin/ReferenceData.razor`
- `src/Modules/XFramework.IdentityServer/IdentityServer.Domain.Shared/Contracts/IdentityRoleTypeGroup.cs`
- `src/Modules/XFramework.IdentityServer/IdentityServer.Domain.Shared/Contracts/IdentityRoleType.cs`
- `src/Modules/XFramework.IdentityServer/IdentityServer.Api/Services/IIdentityAuthorizationService.cs`

## Additional setup gaps confirmed by inspection

A read-only database transaction confirmed **zero chat thread types and zero reaction types** for this new tenant. No active Communications reference-data provisioning endpoint or seed workflow was found in the current source; the legacy entity endpoint list is commented out.

`ThreadService.CreateDirectThreadAsync` requires an existing chat thread type and otherwise returns “Chat thread type not found.” Group creation similarly requires an existing type. These are source findings, not live chat endpoint results: authentication setup prevented reaching those operations.

The chat SDK exposes reaction creation and deletion but no reaction-list method, and the first Yap UI does not yet expose reactions. Reaction E2E coverage needs both valid reference data and an observable read/event path.

The deployed service-identity client list does not contain `XFramework.Yap`. Dedicated client registration, local secret configuration, and connectivity to the loopback-only IdentityServer/Bolt services are still outstanding. Existing Portal credentials were not reused.

## Remaining acceptance checks

After provisioning is repaired:

1. Create a normal tenant member role with only the capabilities needed by Yap, assign Alice and two additional test users, and configure Yap with the actual role ID.
2. Provision tenant-owned chat and reaction types through the owning module, register Yap's service identity, and configure the live app.
3. With separate user sessions, verify direct conversation creation/reuse, bidirectional messages, history after reload, replies, edits, deletion, and realtime delivery.
4. Verify a three-user group, membership visibility, messages to all members, and rejection of a non-member.
5. Verify reaction creation, duplicate behavior, delivery to other members, persistence, and removal.
6. Verify attachment upload, message linking, authorized download, and denied non-member access.

Do not count the fixture tests or container health checks as passes for these live acceptance checks.
