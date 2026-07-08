---
title: "Bolt Hub and Protocol Audit Issues"
date: 2026-07-06
category: workflow-issues
module: Bolt
problem_type: audit
component: bolt_protocol
severity: critical
applies_when:
  - "Hardening Bolt protocol, Bolt Hub routing, service discovery, XFramework integration wrappers, and Portal service-token RPC behavior"
tags: [bolt, bolt-hub, protocol, service-discovery, service-token, portal, audit]
---

# Bolt Hub and Protocol Audit Issues

**Date:** 2026-07-06
**Status:** Implemented through Phase 11
**Scope:** Bolt.Protocol, Bolt.Client, Bolt.Server, Bolt.Hub, XFramework.Integration, generated Bolt handlers, Portal Bolt health, and service-token RPC infrastructure.

This list consolidates the Bolt protocol and Bolt Hub audit findings, plus the Portal/IdentityServer hang investigation around `Role Permission Overrides`. It is an issue inventory, not an implementation plan.

## Implementation Status

**2026-07-06 Phase 1 implemented:** core Bolt/service-token hardening for C1, C2, H1, and related send-queue cleanup was added in `codex/bolt-hub-context`.

Covered in this phase:
- Bounded receive-frame assembly and checked decoded length arithmetic.
- Registration gate for non-register frames and duplicate-register rejection.
- RPC timeout now covers request enqueue/send as well as response wait.
- Client and hub send queues have configurable capacity/enqueue timeout and cleanup queued pooled buffers on cancellation.
- Hub request forwarding removes pending invocations and returns `503` when recipient enqueue fails.
- Routed responses are accepted only from the expected responder without consuming pending calls on spoofed responses.
- Service-token acquisition uses per audience/scope single-flight with self-cleaning inflight entries.
- Portal Bolt health now reports connection, loop, pending-send, active-send, and transport details, and marks stale active sends unhealthy.
- Bolt configuration binds `MaxFrameBytes`, `SendQueueCapacity`, and `SendEnqueueTimeoutMs` for clients and Hub.
- Regression coverage was added for oversized codec lengths, blocked enqueue timeout, full-queue `InvokeAsync`, active-send health degradation, and large RPC streaming paths.

**2026-07-06 Phase 2 implemented:** generated Bolt handler validation parity and service discovery manifest authorization were added in `codex/bolt-hub-context`.

Covered in this phase:
- Generated Bolt handlers now run auto-detected FluentValidation validators before resolving endpoint services or invoking handlers.
- Bolt validation failures return serialized bad-request response envelopes with grouped `ValidationErrors` instead of reaching the service path.
- `BoltRequestContext` now carries the authenticated connection principal for hub-local handlers.
- Bolt service discovery manifest advertisement now requires an authenticated `bolt.service` principal, deterministic client id, matching registered client name, and matching advertised service name.
- Spoofed service manifests are rejected before normalization/persistence, preserving existing records.
- Regression coverage was added for generated Bolt/REST validation output, no-validator generated output, authenticated service manifest advertisement, unauthenticated rejection, authenticated non-service rejection, mismatched service-name rejection, and cross-service registration rejection.

**2026-07-06 Phase 3 implemented:** optional HTTP service-discovery endpoint authorization was added in `codex/bolt-hub-context`.

Covered in this phase:
- Optional `/api/bolt/services` and `/api/bolt/modules` endpoints now require the named `BoltServiceDiscoveryReader` authorization policy instead of being anonymously accessible when `BoltServiceDiscovery:ExposeHttpEndpoints` is enabled.
- Service-discovery HTTP readers are allowed with the `bolt.service` scope, an `*.admin` service scope, or the `Admin` role.
- Regression coverage was added for anonymous rejection, authenticated non-service/non-admin rejection, service-scope access through both `scope` and `scp`, admin-scope access, and admin-role access on both endpoints.

**2026-07-07 Phase 4 implemented:** large RPC short-stream handling was verified and covered with explicit regressions in `codex/bolt-hub-context`.

Covered in this phase:
- Large RPC request reassembly now requires exactly the declared byte count before invoking the target handler.
- Truncated large RPC request streams return `400 BadRequest` over the large-RPC error path and do not call the registered handler.
- Large RPC response reassembly now faults the pending call if the response stream ends before the declared byte count.
- Regression coverage was added for truncated large request streams and truncated large response streams, alongside existing large request/response happy-path coverage.

**2026-07-07 Phase 5 implemented:** Bolt routing integrity hardening for service/command hash collisions, push sender provenance, large-RPC response provenance, and stream-close cleanup was added in `codex/bolt-hub-context`.

Covered in this phase:
- Hub registration now rejects different client IDs that collide on the 32-bit service route hash while still allowing multiple connections for the same client ID.
- Client and Hub command handler/open/send paths now reject different command names that collide on the same 32-bit command hash.
- Hub push routing now validates the frame sender hash against the registered connection before forwarding.
- Large-RPC request streams now bind `requestId` to the actual caller/responder pair at the hub, and large-RPC response push/stream paths cannot complete a pending call from an unexpected responder.
- Stream close cleanup now requires a complete `StreamClose` frame and only removes stream routes when the closer is a stream participant.
- Regression coverage was added for service-hash collision rejection, command-hash collision rejection, spoofed push rejection, unexpected large-RPC response push/stream rejection, truncated stream close, and nonparticipant stream close.

**2026-07-07 Phase 6 implemented:** media stream ownership, call-control authorization, and media recipient concurrency hardening were added in `codex/bolt-hub-context`.

Covered in this phase:
- Media routes now require the original stream owner to send media frames, and require an active call participant before accepting media config or media frames.
- Media feedback/keyframe routing now requires the sender to be an active recipient of the target stream.
- Call signaling now rejects unauthorized answer/reject/end/hold/direct/key-exchange/add/remove operations and duplicate call-id initiation.
- Group-call participant and media-recipient operations now use locked membership helpers and snapshot enumeration before asynchronous sends.
- Disconnect cleanup now treats all call participants consistently, not only the original caller/callee pair.
- Regression coverage was added for non-callee answers, nonparticipant media config, non-owner media frames, non-recipient feedback, and non-owner participant add attempts.

**2026-07-07 Phase 7 implemented:** configuration fallback and WebTransport receive-framing hardening were added in `codex/bolt-hub-context`.

Covered in this phase:
- Bolt Hub database connection resolution now supports `DefaultDatabaseConnection`, `ConnectionStrings:DefaultDatabaseConnection`, `ConnectionStrings:DatabaseConnection`, and `DatabaseConnection` with explicit precedence and a clear missing-config exception.
- WebTransport receive handling now rejects incomplete length prefixes, incomplete message bodies, zero-length messages, and oversized length prefixes without returning partial bytes as usable frame data.
- `AddXFrameworkBoltClient` pool-setting mapping was confirmed with a DI regression for `MinConnections`, `MaxConnections`, and `ScaleUpThreshold`.
- Regression coverage was added for DB connection fallback/missing config, WebTransport fragmented prefix/body handling, incomplete frames, zero-length frames, chunked large messages, and XFramework Bolt pool option mapping.

**2026-07-07 Phase 8 implemented:** client/wrapper correctness fixes for manual routing, unsubscribe, stream cleanup, and lifecycle hooks were added in `codex/bolt-hub-context`.

Covered in this phase:
- `BoltDriver` now normalizes documented XFramework service-name recipients to deterministic SHA client IDs before invoking Bolt, while preserving canonical service names for service-token audience resolution.
- Legacy `BoltDriver.Subscribe(BoltSubscriptionRequest<T>)` calls now own a cancellation source, and `Unsubscribe` cancels the local loop and sends a protocol unsubscribe frame through `BoltClient`.
- Locally closed or remotely closed `BoltStream` instances now remove themselves from the client's `_activeStreams` table through an internal close callback.
- `BoltClient` now raises reconnect/disconnect lifecycle events, and `BoltDriver` bridges them to its existing wrapper callbacks.
- Regression coverage was added for service-name recipient routing, legacy unsubscribe delivery stop, local outbound stream cleanup, and lifecycle callback bridging.

**2026-07-07 Phase 9 implemented:** send-loop cancellation and enqueue cleanup for Hub connections were explicitly verified in `codex/bolt-hub-context`.

Covered in this phase:
- `BoltHubConnection` send-loop cancellation drains queued buffers and restores pending byte accounting.
- Failed enqueue attempts return the rented buffer and keep `PendingBytes` accurate when the send queue is full.
- Regression coverage was added for full-queue enqueue timeout and send-loop cancellation drain behavior on Hub connections.

**2026-07-07 Phase 10 implemented:** durable Redis replay/ack scans and pub/sub client backpressure were hardened in `codex/bolt-hub-context`.

Covered in this phase:
- Redis durable replay now pages with bounded Redis stream-id ranges instead of reading whole streams before applying replay limits.
- Redis durable ack/delete now scans and deletes in bounded batches, stops once the monotonic durable sequence exceeds the ack target, and preserves existing Redis-generated stream IDs for compatibility.
- Durable queue options now expose `RedisStreamScanBatchSize` so the scan page size can be tuned without changing queue retention limits.
- Bolt client transient subscription channels are now bounded by `PubSubChannelCapacity` with an oldest-unread-drop policy so slow transient subscribers cannot create unbounded client memory growth.
- Bolt client durable subscription channels are now bounded without dropping unread entries; overflow fails the local durable subscription so unprocessed persisted entries remain replayable instead of creating ack holes.
- Regression coverage was added for bounded transient pub/sub channel eviction, invalid capacity fallback, durable no-drop overflow behavior, and existing durable replay/ack flows.

**2026-07-07 Phase 11 implemented:** remaining architecture constraints, browser parity, docs, and auth-test gaps were addressed in `codex/bolt-hub-context`.

Covered in this phase:
- Bolt Hub service discovery presence is documented as single-instance only until instance-scoped leases/heartbeats are implemented, and startup now logs that operational constraint.
- The Communications topic authorizer's read-only Identity/Communications access is documented as a narrow approved architecture exception with future owning-module contract direction.
- Bolt wire-format docs and frame comments now describe the 33-byte request header and current pub/sub frame layouts.
- The transport architecture note now states that WebSocket is the current production RPC transport and labels QUIC/WebTransport RPC work as planned rather than implemented defaults.
- The TypeScript browser protocol/client now includes current pub/sub frame constants, Subscribe/Unsubscribe/Publish/Event/Ack encoding and Event decoding/dispatch.
- Regression coverage was added for authorized Bolt WebSocket handshakes, missing-token rejection, and `/bolt/ws` query-token path specificity.

Residual future work:
- M2 horizontal service-discovery presence requires instance-scoped leases/heartbeats before Bolt Hub service discovery can be safely scaled horizontally.
- M3 owning-module authorization contracts/read models remain the preferred future replacement for the documented read-only topic-authorization exception.
- H7 route identifier widening and M8 large-event streaming/payload policy remain future protocol-versioning extensions.

## Critical

### C1. Frame sizing is not consistently bounded

**Status:** Phase 1 implemented.

**Problem:** WebSocket fragments are assembled into growing pooled buffers before codec validation, and several codec reads do unchecked `header + payloadLen` arithmetic. A peer can force memory exhaustion or parser exceptions before the protocol rejects the frame.

**Evidence:**
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:169`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:858`
- `src/Libraries/Bolt/Bolt.Protocol/Protocol/BoltCodec.cs:474`
- Similar unchecked length arithmetic appears around `BoltCodec.cs:504`, `BoltCodec.cs:544`, `BoltCodec.cs:571`, `BoltCodec.cs:617`, `BoltCodec.cs:654`, `BoltCodec.cs:737`, and `BoltCodec.cs:756`.

**Fix direction:**
- Add a protocol-wide configurable max frame/message size.
- Use checked arithmetic for every decoded length calculation.
- Close the connection with a protocol error when limits are exceeded.
- Add malformed/oversized frame tests for request, response, stream, media, register, and pub/sub frames.

### C2. Non-register frames are accepted before successful registration

**Status:** Phase 1 implemented.

**Problem:** `ProcessFrameAsync` dispatches request, response, push, stream, media, pub/sub, and discovery frames before the connection has successfully registered. This bypasses the register identity validation path for non-`Register` frames.

**Evidence:**
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:231`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:297`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:1509`

**Fix direction:**
- Track explicit registration state on `BoltHubConnection`.
- Reject or close every frame except `Register` until registration succeeds.
- Reject duplicate `Register` frames unless an intentional re-registration flow exists.
- Add integration tests for pre-registration `Request`, `Publish`, `StreamOpen`, and `MediaConfig` frames.

## High

### H1. Bolt RPC timeout does not cover blocked enqueue/send, causing shared service-token hangs

**Status:** Phase 1 implemented.

**Problem:** `BoltClient.InvokeAsync` awaits `conn.SendAsync(...)` before registering the RPC timeout. `BoltConnection.SendAsync` writes to a bounded channel with `FullMode.Wait`, so if the send queue is full or the send loop is unhealthy, the configured 30s RPC timeout is never armed. Portal can remain on skeleton loading indefinitely while no IdentityServer handler logs or service-token logs appear.

**Observed scenario:**
- Portal route: `/identity/users/{id}/roles`
- `OpenRolePermissionsDialog` sets `_loadingRolePermissions = true` and awaits `IdentityServer.GetCredentialRolePermissionOverrides`.
- The generated wrapper goes through `BoltDriver.SendAsync`.
- `BoltDriver.EnrichMetadataAsync` obtains a service token before sending the real request.
- On token cache miss, `IdentityServerServiceTokenProvider` sends `IssueServiceTokenRequest` over Bolt.
- If enqueue/send blocks, neither the token request nor the real request reaches IdentityServer, and the response timeout is not active.

**Evidence:**
- `src/Presentation/XFramework.Portal/Components/Pages/Identity/UserDetail.razor:1742`
- `src/Presentation/XFramework.Portal/Components/Pages/Identity/UserDetail.razor:1752`
- `src/Infrastructure/XFramework.Integration/Drivers/BoltDriver.cs:87`
- `src/Infrastructure/XFramework.Integration/Drivers/BoltDriver.cs:251`
- `src/Infrastructure/XFramework.Integration/Security/IdentityServerServiceTokenProvider.cs:61`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:384`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:386`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1278`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1342`
- `src/Presentation/XFramework.Portal/Health/BoltClientHealthCheck.cs:12`
- `src/Infrastructure/XFramework.Integration/Extensions/ServiceCollectionExtensions.cs:100`

**Fix direction:**
- Apply the RPC timeout around send/enqueue and response wait, not only response wait.
- Register timeout before send or use a linked cancellation token that covers `conn.SendAsync`.
- On send/enqueue timeout, remove the pending RPC and return a clear timeout failure.
- Include `PendingSends`, transport connection state, send-loop status, and receive-loop status in health checks.
- Add single-flight token acquisition per audience/scope cache key in `IdentityServerServiceTokenProvider`.
- Add regression tests for a blocked send queue, send-loop failure, token-renewal concurrency, and Portal-facing service-token cache miss behavior.

**Notes:**
- The correct fix belongs in Bolt/service-token infrastructure, not a Portal-only UI timeout.
- No `XFramework.Vault` project was found in this worktree, so Vault-specific impact was not directly verified here. Any app using `AddXFrameworkBoltClient` shares the same `IServiceTokenProvider`, `BoltDriver`, and `BoltClient` infrastructure path.

### H2. Generated Bolt handlers bypass FluentValidation

**Status:** Phase 2 implemented.

**Problem:** The generator detects validators and emits validation for REST adapters, but the Bolt handler path deserializes and invokes the endpoint directly. Bolt calls can accept payloads that HTTP rejects.

**Evidence:**
- `src/SourceGenerators/XFramework.SourceGenerators/BoltHandlerGenerator.cs:224`
- `src/SourceGenerators/XFramework.SourceGenerators/BoltHandlerGenerator.cs:597`
- `src/SourceGenerators/XFramework.SourceGenerators/BoltHandlerGenerator.cs:799`

**Fix direction:**
- Emit validator resolution and `ValidateAsync` in generated Bolt handlers.
- Return the same validation shape and status semantics used by generated REST handlers.
- Add generator tests that assert validator usage in both generated REST and Bolt output.

### H3. Service discovery manifest writes are available to normal authenticated clients

**Status:** Phase 2 implemented.

**Problem:** Any authenticated client that registers a non-reserved identity can advertise a persisted service manifest. The registry only requires a registered `ClientId`, so a normal client can poison service/module discovery.

**Evidence:**
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:1509`
- `src/Modules/XFramework.Bolt/Bolt.Hub/Services/BoltServiceDiscoveryHostedService.cs:44`
- `src/Modules/XFramework.Bolt/Bolt.Hub/Services/BoltServiceDiscoveryRegistry.cs:42`

**Fix direction:**
- Require `bolt.service` or an equivalent service claim for manifest advertisement.
- Cross-check advertised service name against the authenticated service identity.
- Add negative tests for normal authenticated users advertising manifests.

### H4. Optional HTTP discovery endpoints are unauthenticated

**Status:** Phase 3 implemented.

**Problem:** When `BoltServiceDiscovery:ExposeHttpEndpoints` is enabled, `/api/bolt/services` and `/api/bolt/modules` are mapped without endpoint authorization. `UseAuthorization()` alone does not protect anonymous Minimal API endpoints.

**Evidence:**
- `src/Modules/XFramework.Bolt/Bolt.Hub/Extensions/ApplicationBuilderExtension.cs:16`
- `src/Modules/XFramework.Bolt/Bolt.Hub/Extensions/ApplicationBuilderExtension.cs:22`
- `src/Modules/XFramework.Bolt/Bolt.Hub/Extensions/ApplicationBuilderExtension.cs:24`
- `src/Modules/XFramework.Bolt/Bolt.Hub/Extensions/ApplicationBuilderExtension.cs:29`

**Fix direction:**
- Add `.RequireAuthorization()` and preferably a service/admin policy to both discovery endpoints.
- Add endpoint tests with anonymous, normal user, and authorized service/admin callers.

### H5. Large RPC stream reassembly accepts short streams as valid payloads

**Status:** Phase 4 implemented.

**Problem:** Large RPC stream handlers allocate for the declared total size, but do not require `bytesRead == totalSize` before invoking the handler or completing the response. If a peer closes early, consumers can receive unwritten pooled memory as valid data.

**Evidence:**
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:132`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:149`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:254`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:265`

**Fix direction:**
- Fail the stream/RPC unless the exact declared byte count is read.
- Dispose pooled buffers on short reads.
- Add truncated large-request and large-response tests.

### H6. Media routing and call control do not verify sender role

**Status:** Phase 6 implemented.

**Problem:** Media routing trusts `streamId`, and call control mutates/relays by `callId` without consistently verifying that the sender owns the stream or is an authorized participant.

**Evidence:**
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:532`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:567`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:769`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:812`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:880`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:962`

**Fix direction:**
- Verify sender ownership and participant membership before forwarding media frames.
- Restrict answer, end, add participant, and remove participant actions by call role.
- Add negative media/call-control tests.

### H7. Service routing uses 32-bit hashes and trusts client-supplied sender hash

**Status:** Phase 5 implemented with collision/provenance guards. The wire format still uses 32-bit hashes; widening route identifiers remains a future protocol-versioning decision.

**Problem:** Service routing uses a 32-bit FNV-1a hash of `clientId`, and request forwarding does not verify that the frame's `senderHash` matches the registered connection. Collisions or spoofed sender hashes can misroute traffic or confuse large-response routing.

**Evidence:**
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:322`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:393`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:1543`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1083`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1099`

**Fix direction:**
- Add service and command collision detection, or move to a wider stable identifier.
- Derive sender identity server-side during forwarding.
- Add registration-time collision tests.

### H8. Send-loop cancellation can leak queued pooled buffers

**Status:** Phase 9 implemented.

**Problem:** The server send loop uses the request-abort token. Cancellation can exit without draining queued buffers. `SendAsync` increments pending bytes and rents buffers, while the slow path does not guarantee cleanup if the channel is closed or write is canceled.

**Evidence:**
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:1864`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:1889`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:1891`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:1901`

**Fix direction:**
- Complete and drain send queues on close.
- Return all rented buffers on failed enqueue/write.
- Keep `PendingBytes` accurate under cancellation.
- Add cancellation/backpressure tests.

## Medium

### M1. Media recipients list is mutated while enumerated

**Status:** Phase 6 implemented.

**Problem:** `MediaStreamRoute.Recipients` is a mutable `List<T>` enumerated by routing while add/remove/disconnect paths mutate it. Group call changes can throw or corrupt recipient state.

**Evidence:**
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:546`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:613`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:927`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:1001`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:1640`

**Fix direction:** Use locking, immutable snapshots, or a concurrent structure for recipient membership.

### M2. Service discovery presence is single-Hub only

**Status:** Phase 11 documented as a hard single-instance operational constraint. Horizontal service-discovery presence still requires future instance-scoped leases or heartbeats.

**Problem:** `ResetPresenceAsync` runs on every Hub startup and marks all persisted services disconnected, while live connection counts are tracked in local memory. A rolling restart or second Hub instance can mark services connected to another instance offline.

**Evidence:**
- `src/Modules/XFramework.Bolt/Bolt.Hub/Services/BoltServiceDiscoveryHostedService.cs:15`
- `src/Modules/XFramework.Bolt/Bolt.Hub/Services/BoltServiceDiscoveryRegistry.cs:21`
- `src/Modules/XFramework.Bolt/Bolt.Hub/Services/BoltServicePresenceTracker.cs:7`

**Fix direction:** Add Hub instance IDs, leases, or heartbeats; otherwise document single-instance Bolt Hub as a hard constraint.

### M3. Bolt Hub directly reads Identity and Communications schemas for topic authorization

**Status:** Phase 11 documented as a narrow approved read-only architecture exception, with owning-module authorization contracts/read models left as the preferred future direction.

**Problem:** The Hub references other modules' shared domain projects and queries Identity and Communications entities directly for topic authorization. This is an undocumented cross-module read path in a security-sensitive surface.

**Evidence:**
- `src/Modules/XFramework.Bolt/Bolt.Hub/Bolt.Hub.csproj:14`
- `src/Modules/XFramework.Bolt/Bolt.Hub/Bolt.Hub.csproj:15`
- `src/Modules/XFramework.Bolt/Bolt.Hub/Services/CommunicationsBoltTopicAuthorizer.cs:37`
- `src/Modules/XFramework.Bolt/Bolt.Hub/Services/CommunicationsBoltTopicAuthorizer.cs:125`

**Fix direction:** Move behind owning-module authorization contracts/read models, or document it as an approved exception.

### M4. Redis durable queue replay and ack scan full streams

**Status:** Phase 10 implemented.

**Problem:** Redis replay reads the whole stream before applying `maxCount`, and ack/delete scans the full stream. At default queue sizes this can become a Redis and network hot path.

**Evidence:**
- `src/Libraries/Bolt/Bolt.Server/PubSub/RedisDurableQueueStore.cs:67`
- `src/Libraries/Bolt/Bolt.Server/PubSub/RedisDurableQueueStore.cs:99`
- `src/Libraries/Bolt/Bolt.Server/PubSub/DurableQueueOptions.cs:15`

**Fix direction:** Use bounded `XRANGE`/stream-id reads and indexed ack metadata, and add Redis-backed contract tests.

### M5. Manual BoltDriver routing can 404 for documented service-name recipients

**Status:** Phase 8 implemented.

**Problem:** `BoltDriver` documents `recipient` as a service name or direct client ID, but sends the value raw to `InvokeAsync`. The Hub registers deterministic SHA client IDs. Generated wrappers pre-hash correctly; direct callers using service names can hash the wrong value and get false 404s.

**Evidence:**
- `src/Infrastructure/XFramework.Integration/Drivers/BoltDriver.cs:21`
- `src/Infrastructure/XFramework.Integration/Drivers/BoltDriver.cs:71`
- `src/Infrastructure/XFramework.Integration/Extensions/ServiceCollectionExtensions.cs:65`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:320`

**Fix direction:** Normalize recipients consistently before invoking Bolt, or narrow the contract and tests to direct client IDs only.

### M6. Unsubscribe is a no-op and closed outbound streams can remain tracked

**Status:** Phase 8 implemented.

**Problem:** `IMessageBusWrapper.Unsubscribe` sends no protocol frame and owns no cancellation token, so legacy callers can continue receiving events until connection teardown. Separately, locally closed outbound streams remain in `_activeStreams` until the peer sends close.

**Evidence:**
- `src/Infrastructure/XFramework.Integration/Drivers/BoltDriver.cs:180`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:742`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:801`
- `src/Libraries/Bolt/Bolt.Client/BoltStream.cs:151`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1025`

**Fix direction:** Implement protocol unsubscribe for wrapper callers and remove locally closed streams from client state.

### M7. Local/non-compose Bolt Hub database config uses a different key

**Status:** Phase 7 implemented.

**Problem:** `DbInstaller` reads `DefaultDatabaseConnection`, while Bolt Hub appsettings define `ConnectionStrings:DatabaseConnection`. Docker Compose supplies `DefaultDatabaseConnection`, but direct local/staging execution can fail startup or health checks.

**Evidence:**
- `src/Modules/XFramework.Bolt/Bolt.Hub/Installers/DbInstaller.cs:19`
- `src/Modules/XFramework.Bolt/Bolt.Hub/appsettings.json:17`
- `src/Modules/XFramework.Bolt/Bolt.Hub/appsettings.Development.json:20`
- `src/Modules/XFramework.Bolt/Bolt.Hub/appsettings.Staging.json:17`
- `src/Modules/XFramework.Bolt/Bolt.Hub/appsettings.Docker.json:2`

**Fix direction:** Standardize the connection key or support both with a clear precedence rule.

### M8. Pub/sub large payloads are copied and queued without backpressure

**Status:** Phase 10 implemented for bounded client subscription queues. Large-event streaming/payload policy remains a future protocol extension if required.

**Problem:** Publish serializes a whole payload into one frame, the Hub copies it again, and subscriber channels are unbounded. Large fan-out or slow subscribers can create memory pressure despite codec caps.

**Evidence:**
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:595`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:546`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:759`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:1303`

**Fix direction:** Add bounded subscriber channels, backpressure/drop policy, payload size policy, and streaming support for large events if required.

### M9. Response routing tracks only caller, not expected responder

**Status:** Phase 5 implemented.

**Problem:** Pending RPC state stores only the caller and timestamp. Any connected client that can produce the same `RequestId` can complete a pending invocation. GUID guessing is low probability, but expected recipient tracking would catch spoofing and misroutes.

**Evidence:**
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:36`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:399`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:424`

**Fix direction:** Store the expected recipient/stream ID with each pending invocation and verify the responder before forwarding the response.

### M10. WebTransport length-prefix handling can return partial messages as usable data

**Status:** Phase 7 implemented.

**Problem:** WebTransport receive handling treats partial prefixes/bodies as usable data, and zero-length messages can return `(0, false)`, which risks receive-loop spinning or malformed frames.

**Evidence:**
- `src/Libraries/Bolt/Bolt.Client/Transport/WebTransportBoltConnection.cs:76`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:175`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:859`

**Fix direction:** Fully buffer length prefixes and exact message bodies inside the transport before returning frame bytes.

### M11. `CleanupStream` uses buffer capacity instead of received length

**Status:** Phase 5 implemented.

**Problem:** `CleanupStream` checks `buffer.Length`, not the actual frame length, then reads a stream ID from pooled buffer contents. A truncated `StreamClose` can remove an unrelated stream.

**Evidence:**
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:516`

**Fix direction:** Pass received length into cleanup and only read the stream ID when the received frame is large enough.

### M12. Reconnect lifecycle hooks exposed by BoltDriver do not fire

**Status:** Phase 8 implemented.

**Problem:** `BoltDriver` exposes reconnect/disconnect hooks, but the current client reconnect flow does not wire them. Consumers migrated from the wrapper contract cannot observe state changes.

**Evidence:**
- `src/Infrastructure/XFramework.Integration/Drivers/BoltDriver.cs:32`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1119`

**Fix direction:** Wire Bolt client lifecycle events into the wrapper hooks or remove the public contract.

## Low

### L1. Protocol docs and comments still describe stale request headers

**Status:** Phase 11 implemented.

**Problem:** Several docs/comments still describe a 29-byte request header, while the codec uses a 33-byte header with `senderHash`.

**Evidence:**
- `src/Libraries/Bolt/BOLT.md:84`
- `src/Libraries/Bolt/Bolt.Protocol/Protocol/BoltCodec.cs:12`
- `src/Libraries/Bolt/Bolt.Protocol/Protocol/BoltCodec.cs:24`

**Fix direction:** Update docs/comments and add a protocol version table or generated wire-format reference.

### L2. Transport docs promise QUIC/WebTransport defaults that current code does not expose

**Status:** Phase 11 implemented by correcting the durable architecture note to match the current WebSocket-first RPC runtime.

**Problem:** Durable docs say QUIC/WebTransport/WebSocket are available and QUIC is default, but current source defaults to WebSocket and the server maps WebSocket only.

**Evidence:**
- `docs/solutions/architecture-patterns/bolt-unified-transport-layer.md:22`
- `src/Libraries/Bolt/Bolt.Client/BoltClientOptions.cs:16`
- `src/Libraries/Bolt/Bolt.Client/Transport/BoltTransportNegotiator.cs:30`
- `src/Libraries/Bolt/Bolt.Server/BoltServerExtensions.cs:68`

**Fix direction:** Either implement the documented transport matrix or update the docs to mark QUIC/WebTransport as planned.

### L3. Browser protocol client is not at parity with current pub/sub frames

**Status:** Phase 11 implemented.

**Problem:** The TypeScript browser client claims protocol parity but does not include current pub/sub frame types or event handling.

**Evidence:**
- `src/Libraries/Bolt/Bolt.Browser/src/protocol.ts`
- `src/Libraries/Bolt/Bolt.Browser/src/bolt-client.ts`

**Fix direction:** Add pub/sub frame definitions and handlers, or narrow package docs to its supported surface.

### L4. Auth/protocol integration tests do not mirror production auth behavior

**Status:** Phase 11 implemented.

**Problem:** Production Portal/Bolt Hub uses `RequireAuthorization`, and query-token extraction is path-specific, but Bolt pub/sub integration tests map `MapBolt` without auth. This leaves handshake token handling and identity enforcement under-covered.

**Evidence:**
- `src/Modules/XFramework.Bolt/Bolt.Hub/Extensions/ApplicationBuilderExtension.cs:20`
- `src/Kernel/XFramework.Core/Extensions/InstallerExtensions.cs:130`
- `src/Tests/Bolt.Tests/BoltPubSubIntegrationTests.cs:42`

**Fix direction:** Add end-to-end tests for authorized and unauthorized Bolt handshakes, register identity enforcement, and actor token authorization.

### L5. BoltConfiguration pool settings are ignored by AddXFrameworkBoltClient

**Status:** Phase 7 implemented.

**Problem:** Config exposes pool settings and the client builder supports them, but XFramework integration registration only applies server, identity, timeout, and token options.

**Evidence:**
- `src/Shared/XFramework.Domain.Shared/Configurations/BoltConfiguration.cs:23`
- `src/Libraries/Bolt/Bolt.Client/BoltClientExtensions.cs:33`
- `src/Infrastructure/XFramework.Integration/Extensions/ServiceCollectionExtensions.cs:71`

**Fix direction:** Map `MinConnections`, `MaxConnections`, and `ScaleUpThreshold` from configuration into the Bolt client builder.

## Open Questions

- Is Bolt Hub intended to support horizontal scaling now, or is single-instance deployment a formal constraint?
- Should service discovery be visible to normal authenticated users, or only service/admin identities?
- Should protocol compatibility target external implementers now, or can docs describe the .NET client as the only supported implementation?
- Does the separate Vault branch/project use `AddXFrameworkBoltClient`? It is not present in this worktree, but shared infrastructure risk applies if it does.

## Suggested First Fix Order

1. Gate non-register frames and add hard frame/message limits.
2. Cover `conn.SendAsync` and service-token acquisition with real timeouts, and add health details for send/receive loop state.
3. Add single-flight service-token acquisition and blocked-send regression tests.
4. Fix generated Bolt validation parity with REST.
5. Lock down service discovery writes and optional HTTP discovery endpoints.
6. Repair large-RPC short-read handling and send-loop buffer cleanup.
