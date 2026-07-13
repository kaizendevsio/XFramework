---
title: "Bolt Protocol, Hub, and Media Audit"
date: 2026-07-13
category: workflow-issues
module: Bolt
problem_type: audit
component: transport
severity: critical
applies_when:
  - "Auditing Bolt protocol, Bolt Hub, or Bolt Media correctness, security, performance, and scalability"
  - "Planning remediation or certification work for the Bolt transport stack"
tags: [bolt, bolt-hub, bolt-media, protocol, security, performance, audit]
status: active
---

# Bolt Protocol, Hub, and Media Audit - 2026-07-12

**Status:** Open findings; no production code changed by this audit
**Baseline:** `origin/develop` at `18189df6fc486733819c49d8d8095033247d3186`
**Worktree:** `C:\Users\Xeon\RiderProjects\XFramework-worktrees\bolt-hub-context`
**Scope:** `Bolt.Protocol`, `Bolt.Client`, `Bolt.Server`, Bolt Hub, durable pub/sub, XFramework integration/authentication, `Bolt.Media`, `Bolt.Media.Browser`, TypeScript browser client, tests, benchmarks, and deployment configuration.

## Executive Assessment

Bolt is promising, but it is not ready to support a defensible claim that it is faster or more memory efficient than every competing transport.

The current localhost benchmark favors Bolt: the sequential thin-protocol cases are roughly twice as fast as the corresponding gRPC cases and allocate materially less in the measured method. That result is useful for local regression tracking, but the harness is not a fair protocol comparison. It compares raw Bolt responses with typed gRPC/SignalR decoding, gives Bolt more parallel connections, measures batches as operations in several reports, excludes connection setup memory, and does not cover TLS, multi-host latency, loss, tuned gRPC channels, HTTP/2 flow-control tuning, or streaming.

More importantly, confirmed security, lifecycle, backpressure, and Bolt Media defects can cause credential interception, cross-request response corruption, memory exhaustion, connection-wide stalls, silent send loss, or nonfunctional media. These must be resolved before optimizing the remaining hot paths.

### Finding Count

| Severity | Count | Meaning |
|---|---:|---|
| Critical | 1 | Direct trust/data-integrity compromise with a practical authenticated attack path |
| High | 23 | Major security, correctness, availability, scalability, or performance-goal blocker |
| Medium | 17 | Important functional, operational, interoperability, or bounded performance defect |
| Low | 5 | Documentation, diagnostics, test, or maintainability gap |

## Critical

### C1. Reserved service identities can be joined and intercepted

The Hub resolves a missing registration binding mode to `Audit`, and every checked-in Hub configuration omits an override. In Audit mode, identity mismatches are logged and accepted. Any principal that can authenticate to `/bolt/ws` can register a known reserved service client ID and name, join that service's round-robin connection bag, receive internal RPC requests, and submit responses accepted as the selected responder.

This includes IdentityServer service-token requests whose payload contains a long-lived service `ClientSecret`. The result is credential theft, forged service responses, denial of service, and durable lateral compromise.

Evidence:

- `src/Modules/XFramework.Bolt/Bolt.Hub/Installers/BoltInstaller.cs:37-40,78-90`
- `src/Libraries/Bolt/Bolt.Server/BoltServerExtensions.cs:27-32`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:408-449,528-548,2022-2077,2117-2150`
- `src/Modules/XFramework.Bolt/Bolt.Domain.Shared/Contracts/ServiceIdentity/ServiceIdentityContracts.cs:12-15`
- `src/Infrastructure/XFramework.Integration/Security/IdentityServerServiceTokenProvider.cs:98-116`

Required direction: default and fail closed to `Enforce` outside Development, reject user principals from reserved service registration, derive routable identity from validated claims, and test hostile duplicate registration against every reserved service route.

## High

### H1. Pooled RPC completion objects have an ABA/reuse race

`PooledRpcCall.GetResult` returns the completion source to the object pool before `BoltClient.InvokeAsync` removes the old request ID from `_pendingCalls`. A timeout, failure, or duplicate/late response can therefore observe the old dictionary entry after the same object has been rented and reset for a new request. The old response may complete the new request with another caller's data.

This is a cross-request data-integrity and confidentiality defect, not only a performance race. It is High rather than Critical because exploitation requires a narrow timeout/re-rent/late-response interleaving.

Evidence:

- `src/Libraries/Bolt/Bolt.Client/PooledRpcCall.cs:21-25,31-41,43-70,87-91`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:479-544,556-620,1191-1212`

Required direction: make pending-entry ownership generation-aware and remove the exact `(requestId, completion)` registration before pool return. Add deterministic timeout/late-response/re-rent tests and duplicate response tests.

### H2. Authenticated peers can force byte-unbounded Hub memory growth

The default protocol accepts frames up to 100 MB. Hub queues are bounded by message count, not bytes, and each fanout recipient receives another pooled copy. Media routing validates only a small prefix before fanout, and non-drop-eligible frames wait on recipient queues. A small number of authenticated call participants can queue many large frames to non-reading recipients, retain gigabytes of arrays, stall receive loops, and exhaust the Hub.

Configured `MaxPendingRpcCalls` and `MaxParallelInvocationsPerClient` are not wired into `BoltServer`, so ordinary RPC pressure also grows unbounded pending state until cleanup. This is scoped to authenticated peers because the XFramework Hub endpoint requires authorization.

Evidence:

- `src/Libraries/Bolt/Bolt.Server/BoltServerExtensions.cs:18-25`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:37-41,223-298,951-1017,2441-2539`
- `src/Shared/XFramework.Domain.Shared/Configurations/BoltConfiguration.cs:19,23-25`
- `src/Modules/XFramework.Bolt/Bolt.Hub/Installers/BoltInstaller.cs:25-40`

Required direction: use protocol-specific frame ceilings, byte/rate/concurrency budgets per principal and call, global pending limits, streaming for large RPC payloads, and nonblocking media fanout with an explicit drop policy. Stress tests must include 100 MB input, many queued frames, and a non-reading recipient.

### H3. `Enforce` cannot isolate services while all services share the JWT signing secret

Every service receives the same symmetric JWT secret and locally generates its Bolt `bolt.service` access token. A compromised low-privilege service can mint a valid token whose service claims name any other service, so switching registration binding to `Enforce` does not create a real service trust boundary.

Evidence: `docker-compose.yml:13-22`; `src/Infrastructure/XFramework.Integration/Extensions/ServiceCollectionExtensions.cs:100-107,126-163`; `src/Kernel/XFramework.Core/Extensions/InstallerExtensions.cs:109-155`.

Required direction: use short-lived asymmetric IdentityServer-issued transport tokens, workload identity, or mTLS; separate user and service authentication schemes; never distribute a Hub-verification signing key to callers.

### H4. Checked-in deployment paths use plaintext WebSockets

Compose clients use `ws://`, the Hub exposes HTTP directly, bearer access tokens are placed in the WebSocket query string, and Bolt carries actor tokens, RPC data, and service secrets over that connection. A network observer or compromised workload on the segment can capture reusable credentials and content.

Evidence: `src/Modules/XFramework.Bolt/Bolt.Hub/Extensions/ApplicationBuilderExtension.cs:15-21`; `src/Modules/XFramework.Bolt/Bolt.Hub/appsettings.Staging.json:34-41`; `docker-compose.yml:13-25,97-120,458-465`.

Required direction: require `wss://` through a trusted TLS endpoint, preferably mTLS for services, remove direct plaintext exposure, redact query tokens from all access logs, and fail startup outside Development when the transport is plaintext.

### H5. Custom frame buffers are rented and never returned

The client receive loop rents and copies a buffer for every custom frame, transfers ownership to the handler, and does not return it. The built-in Event handler and every Bolt Media handler fail to return that buffer; most media handlers then allocate another payload array. This defeats pooling and creates sustained allocation/GC pressure at media frame rates.

Evidence: `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:62-63,144,698-742,1145-1153`; `src/Libraries/Bolt/Bolt.Media/BoltMediaClient.cs:48-55,141-215`.

Required direction: replace the ambiguous callback contract with scoped/ref-counted frame ownership or return the buffer in a guaranteed `finally`; eliminate the second payload copy where lifetime permits.

### H6. Large RPC replies lose connection affinity in multi-connection clients

The large-RPC receive handler chooses arbitrary pool connections for error/small replies. For a large reply it constructs `BoltStream` with one `GetConnection()` call and sends `StreamOpen` with another. The Hub binds a stream to its opening connection and rejects chunks from a different owner, so responses intermittently fail once a service has more than one Bolt connection.

Evidence: `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:172-204,259-304`; `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:689-760`.

Required direction: expose the inbound stream's owning connection and use it for the complete reverse response. Add tests with at least two connections on both caller and responder.

### H7. Enqueue success is reported as send success and transport failures are swallowed

`BoltConnection.SendAsync` completes when a copied frame enters the channel, not when the transport sends it. The send loop catches cancellation and transport errors without notifying the originating RPC. Caller cancellation after enqueue can silently discard the request while `InvokeAsync` waits for its independent RPC timeout. Hub sends have the same error-swallowing behavior.

Evidence: `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:490-544,1547-1663`; `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:2441-2539`.

Required direction: carry a pooled completion/ack with important sends, fail and retire unhealthy connections on transport error, link caller cancellation to pending completion, and distinguish enqueue, transport-send, and response timeout metrics.

### H8. One unread logical stream can stall an entire multiplexed connection

Each `BoltStream` has a 1,024-item bounded channel in `Wait` mode. The shared receive loop awaits `EnqueueInboundAsync`; once one consumer stops reading, all RPC responses, pushes, events, and other streams on that physical connection stop being processed. Unknown stream opens are also retained without a handler or admission limit.

Evidence: `src/Libraries/Bolt/Bolt.Client/BoltStream.cs:37-47,166-173`; `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1214-1250`.

Required direction: enforce per-stream byte windows and deadlines, never await a logical consumer from the transport demultiplexer, reject unknown stream commands, and reset only the offending stream on overflow.

### H9. Request, push, stream, and media dispatch lacks admission control

Inbound requests and pushes are launched as untracked tasks; stream handlers use `Task.Run`; media configuration can create/replace unlimited stream controllers and periodic tasks. There are no per-connection concurrency, task, stream, config-rate, or byte budgets.

Evidence: `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1122-1134,1180-1189,1214-1227`; `src/Libraries/Bolt/Bolt.Media/BoltMediaClient.cs:154-176`; `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:1045-1065`.

Required direction: add bounded dispatch schedulers and per-principal quotas, reject excess work explicitly, and dispose replaced controllers/streams before replacement.

### H10. Media sequence gaps and NACKs permit algorithmic amplification

Both C# and browser receivers expand every missing sequence number one by one. A jump near `uint.MaxValue` can execute billions of iterations, allocate enormous sets/PLC frames, and hang or OOM a receiver. NACK decoding accepts up to 65,535 entries and duplicates; repeating a buffered sequence can amplify one request into thousands of large retransmits. Bandwidth probes also use near-maximum sequence values in the normal data path.

Evidence: `src/Libraries/Bolt/Bolt.Media/NackTracker.cs:56-83`; `src/Libraries/Bolt/Bolt.Media/BoltMediaStream.cs:328-342,384-418`; `src/Libraries/Bolt/Bolt.Media/BandwidthProber.cs:104-118`; `src/Libraries/Bolt/Bolt.Browser/src/media-stream.ts:112-180`; `src/Libraries/Bolt/Bolt.Protocol/Protocol/BoltCodec.cs:788-803`.

Required direction: use wrap-aware bounded windows, cap NACKs to the intended small count, deduplicate and rate/byte-limit retransmits, and separate probe/control sequence space.

### H11. FEC is functionally incorrect and retains groups indefinitely

C# records each source frame with `groupStart = sequence` and attempts recovery under the missing sequence, while parity uses the actual group start. Browser code discards normal source frames received before parity. Neither runtime expires incomplete groups. FEC therefore fails common recovery paths and retains copied packet data indefinitely; encrypted recovered data can also bypass normal decryption.

Evidence: `src/Libraries/Bolt/Bolt.Media/BoltMediaStream.cs:384-421`; `src/Libraries/Bolt/Bolt.Media/FecDecoder.cs:5-47`; `src/Libraries/Bolt/Bolt.Browser/src/media-stream.ts:147-165`.

Required direction: retain a bounded recent-packet window, map parity to real groups, validate lengths, expire groups, and feed recovered ciphertext through authenticated decryption.

### H12. The high-level Blazor Bolt Media service does not send or play real media

The service constructs outgoing streams directly but never inserts them into `BoltMediaClient._mediaStreams`; encoded callbacks look up those IDs and receive `null`. Playback loops are attached to outgoing streams rather than remotely announced streams, and the answering side does not run the setup path used by `OnCallAnswered`.

Evidence: `src/Libraries/Bolt/Bolt.Media.Browser/BoltMediaService.cs:138-154,200-275`; `src/Libraries/Bolt/Bolt.Media/BoltMediaClient.cs:116-136,224-254`.

Required direction: provide explicit registered outgoing-stream and remote-stream-added APIs, initialize both peers on active-call transition, and add a two-browser encode-to-remote-decode test.

### H13. Media encryption silently downgrades and lacks authenticated key exchange

Calls requested as encrypted send plaintext until key derivation completes, while early encrypted frames may be delivered as codec data when no key is ready. ECDH public keys are relayed without signatures, identity binding, transcript binding, or key confirmation, so the Hub can substitute keys despite the E2E claim. Blazor also uses one mutable JavaScript AES-key object, allowing a second call to overwrite the first call's key, and blocks synchronously on asynchronous JS crypto.

Evidence: `src/Libraries/Bolt/Bolt.Media/BoltMediaStream.cs:260-270,351-373`; `src/Libraries/Bolt/Bolt.Media/MediaEncryption.cs:70-83,186-220`; `src/Libraries/Bolt/Bolt.Media.Browser/BoltCryptoInterop.cs:9-48`; `src/Libraries/Bolt/Bolt.Media.Browser/wwwroot/bolt-crypto.js:27-77`; `src/Libraries/Bolt/Bolt.Browser/src/encryption.ts:47-87`.

Required direction: make encryption policy explicit and fail closed, authenticate and confirm the key transcript, isolate crypto state per call/sender, make the media crypto path asynchronous, and add concurrent-call/replay/downgrade tests.

### H14. Group media membership and key state cannot converge reliably

New participants receive neither existing media configs nor a membership snapshot. Removed participants are deleted before the removal signal is sent to them. Pairwise key exchanges overwrite one per-call key, so an encrypted group cannot converge on a stable group or sender-key epoch.

Evidence: `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:1401-1475,1482-1532`; `src/Libraries/Bolt/Bolt.Media/BoltMediaClient.cs:224-254`.

Required direction: versioned membership epochs, join/leave acknowledgements, config replay, target removal notification, and per-sender or group-key rotation.

### H15. Receive buffers retain excessive memory per connection

Client and server rent 256 KB for every physical connection for its entire lifetime. A fragmented frame grows `largeBuffer` up to the 100 MB frame limit and retains that largest array until another fragmented frame or disconnect. A single large message can therefore pin roughly 100 MB per idle connection in the shared pool.

Evidence: `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:216-312`; `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1038-1163`.

Required direction: start with a small receive buffer, return large assembly buffers immediately after dispatch, stream large frames, and expose retained receive bytes in health/metrics.

### H16. Hot paths make multiple full-frame copies and defer pool return to finalizers

Typed calls serialize into a writer, frame construction copies payload, enqueue copies the entire frame, Hub fanout copies it again per recipient, and custom/media dispatch copies again. Stream chunks use a finalizable `MemoryManager`; consumers cannot deterministically return its array. This architecture is not zero-copy and will hit memory bandwidth and GC at large payload or fanout workloads.

Evidence: `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:623-644,1122-1153,1230-1239,1621-1633`; `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:2499-2511,2611-2616`; `src/Libraries/Bolt/Bolt.Protocol/Buffers/PooledMemoryOwner.cs:9-57`.

Required direction: benchmark copy bytes/op, adopt explicit/ref-counted pooled frame ownership, scatter/gather where transports permit, and remove per-chunk finalizers from the normal path.

### H17. The wire protocol has no version/capability negotiation and relies on 32-bit command IDs

Registration exchanges identity plus a Boolean acknowledgement only. Peers cannot negotiate protocol version, frame extensions, limits, codecs, compression, or transport capability. Commands are represented solely by 32-bit FNV-1a hashes; local collision guards cannot detect a different colliding command name on another peer. Rolling upgrades fail late or can invoke the wrong handler.

Evidence: `src/Libraries/Bolt/Bolt.Protocol/Protocol/BoltCodec.cs:29-115,1113-1125`; `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:647-650,961-979`; `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:176-204,393-456`.

Required direction: introduce a versioned handshake with capabilities and negotiated limits, and use wider stable command/service IDs or exchange a collision-checked command table per session.

### H18. Topic authorization is default-allow and durable subscriber cardinality is unbounded

Unknown topic namespaces are allowed. Communications subscriber validation checks only a prefix, permitting many arbitrary subscriber IDs. Durable publish enumerates all subscriber identities and performs Redis work per identity; stale membership has no individual lease. An authenticated client can create unbounded Hub/Redis state and publish amplification.

Evidence: `src/Modules/XFramework.Bolt/Bolt.Hub/Services/CommunicationsBoltTopicAuthorizer.cs:21-24,102-113`; `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:1649-1655,1802-1841`; `src/Libraries/Bolt/Bolt.Server/Durable/RedisDurableQueueStore.cs:37-38,60-62,170-191`.

Required direction: default-deny namespaces, strict topic/subscriber grammar, per-principal leases/quotas, bounded replay, and cardinality/amplification tests.

### H19. Communications actor-token authorization uses the wrong tenant context

The authorizer resolves actor identity but EF global filters still use the ambient WebSocket service principal, which has no tenant claim. The query can throw; the exception bubbles out of frame processing and closes the shared service connection, disrupting unrelated RPC traffic.

Evidence: `src/Modules/XFramework.Bolt/Bolt.Hub/Services/CommunicationsBoltTopicAuthorizer.cs:33-46,116-180`; `src/Kernel/XFramework.Domain/Contexts/XDbContext.cs:81-127`; `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:298-313`.

Required direction: query an explicit actor/tenant read model with explicit tenant and soft-delete predicates, contain authorization failure to one frame, and cover with PostgreSQL-backed service-principal tests.

### H20. Multiple Hub replicas break routing, live pub/sub, and presence

Routes, calls, media, and live subscriptions are process-local; Redis is only a durable queue. Each Hub replica resets shared presence at startup and writes counts based only on its local connections. Splitting clients across replicas causes 404 routing, lost transient events, delayed durable delivery, and flapping presence.

Evidence: `src/Modules/XFramework.Bolt/Bolt.Hub/Services/BoltServiceDiscoveryHostedService.cs:13-22`; `src/Modules/XFramework.Bolt/Bolt.Hub/Services/BoltServicePresenceTracker.cs:7-33`; `src/Modules/XFramework.Bolt/Bolt.Hub/Services/BoltServiceDiscoveryRegistry.cs:18-36,141-156`; `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:37-40,77-85,528-534`.

Required direction: enforce one replica until an instance-aware backplane/lease design exists, then prove all modes in two-process tests.

### H21. Service discovery grows without bound and performs quadratic work

Every registered client becomes a persisted service/presence record, including ordinary user/media clients. Records and tracker states are not retired, default reads include offline history, and dependency/manifests sets are rebuilt repeatedly for each record. Churn creates database writes and unbounded state; registry evaluation trends toward O(N squared).

Evidence: `src/Modules/XFramework.Bolt/Bolt.Hub/Services/BoltServiceDiscoveryHostedService.cs:34-35`; `src/Modules/XFramework.Bolt/Bolt.Hub/Services/BoltServiceDiscoveryRegistry.cs:123-203,234-322,363-390`; `src/Modules/XFramework.Bolt/Bolt.Hub/Services/BoltServicePresenceTracker.cs:15-27`.

Required direction: persist only validated advertised services, expire empty presence, paginate/retain history, and precompute an immutable registry snapshot.

### H22. The current benchmark cannot support the published performance claims

The concurrent benchmark reports batches per second as RPC operations per second because it lacks `OperationsPerInvoke`. The throughput suite reports batch-amortized wall time as request latency. Raw Bolt results do not deserialize the response while typed gRPC/SignalR do. Bolt uses 4-8 connections while comparison transports generally use one. Setup memory is outside `MemoryDiagnoser`, percentile values are computed from a handful of iteration aggregates, and the suite is localhost/in-process/plaintext.

Evidence: `src/Tests/Bolt.Tests/BoltBenchmarks.cs:53-54,241-310,387-390,550-663`; `src/Libraries/Bolt/BOLT.md:29-47`.

Required direction: correct operation accounting; compare equivalent typed/raw work; equalize or explicitly sweep channels; use secure multi-process/multi-host tests; tune gRPC's documented channel, HTTP/2 flow-control, and streaming modes; collect request-level HDR p50/p95/p99/p99.9 under open-loop load and soak.

### H23. RPC deadlines and cancellation do not propagate to the remote handler

Caller cancellation and the configured RPC timeout stop only the local wait. The Request frame carries request ID, route hashes, command hash, and payload length, but no deadline or cancellation contract. Once enqueued, the Hub and receiving client continue routing and executing the request under connection-lifetime cancellation; the caller cannot cancel remote handler or downstream work, and a late response may arrive after the local request has timed out and released its budget. This wastes work under overload and prevents semantic parity with deadline-aware transports such as gRPC.

Evidence:

- `src/Libraries/Bolt/Bolt.Protocol/Protocol/BoltCodec.cs:46-70`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:495-575,1206-1328`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:595-741`

Required direction: contain late responses and release local budgets immediately in the wire-compatible lifecycle phase, then add negotiated absolute deadlines and request-cancellation frames in Protocol v2. The Hub and receiver must reject expired work before dispatch, cancel the exact active handler/downstream token, make duplicate/late cancellation idempotent, and prove bounded cleanup under races.

## Medium

### M1. Connection creation has no end-to-end registration deadline

Transport connection cancellation does not guarantee a deadline across register send and acknowledgement. Failure paths can leave the WebSocket and acknowledgement buffer undisposed, and a server that accepts but never acknowledges can hold startup indefinitely.

Evidence: `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:384-409`; transport implementations under `src/Libraries/Bolt/Bolt.Client/Transport`.

### M2. Failed pool members are not reliably retired or restored

Connection failure/removal, reconnect, and background scale-up can overlap. Scale-up has no single-flight gate or scale-down; disposed clients can be repopulated by a late task, and removed connections do not consistently complete all stream/subscription state.

Evidence: `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:147-162,412-470,1012-1035,1160-1175,1338-1425`.

### M3. Recipient selection allocates/scans and removal can lose concurrent additions

`ConcurrentBag` is enumerated twice per routed request to count and select a recipient. Bag enumeration snapshots and scales with all connections for a service. Disconnect reconstructs and replaces the bag while a concurrent registration can append to the old bag, losing the newly added connection.

Evidence: `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:445-449,2117-2171`.

### M4. Protocol writers can emit frames their own readers reject

Payload limits are checked inconsistently relative to header size; empty publish and oversized strings/tokens can be written but rejected on read. NACK count is cast to `ushort` while all supplied entries are appended, so 65,536 wraps the header count to zero with trailing data.

Evidence: codec writer/reader pairs in `src/Libraries/Bolt/Bolt.Protocol/Protocol/BoltCodec.cs`, especially media NACK and pub/sub sections.

### M5. Browser codec is allocation-heavy and not at .NET safety parity

Browser encode/decode creates frame arrays/DataViews, copies payloads, and repeatedly parses UUID text. It lacks equivalent global frame enforcement. UUID parsing maps invalid hex to zeros, and converting JavaScript `number` to `BigInt` loses 64-bit precision above `Number.MAX_SAFE_INTEGER`.

Evidence: `src/Libraries/Bolt/Bolt.Browser/src/protocol.ts` and related browser protocol files.

### M6. Media processor/recording input is incorrect

`IMediaProcessor.Accepts` is not applied, processors receive the complete Bolt frame instead of encoded payload, FEC can be parsed as media, and disconnect cleanup omits `OnCallEndedAsync`. Filters and recordings can therefore be invalid and file handles/state can survive disconnect.

Evidence: `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:1008-1017,1535-1552,2184-2212`; `src/Libraries/Bolt/Bolt.Server/Media/RecordingProcessor.cs:41-63`.

### M7. Call signaling omits required identity and capability data

The `video` request is not serialized, incoming caller identity is empty, `VideoRequested` is hard-coded false, and codec negotiation is not used in production. The signaling format cannot express directional capabilities or encryption requirements.

Evidence: `src/Libraries/Bolt/Bolt.Media/BoltMediaClient.cs:65-80,224-230`; `src/Libraries/Bolt/Bolt.Media/CodecNegotiation.cs:9-20`.

### M8. Media timestamps and codec metadata are inconsistent

RTP-style ticks are passed directly to WebCodecs, which expects microseconds. Selecting H.265 can still advertise H.264, and decoder configuration metadata is discarded, breaking A/V timing and codec interoperability.

Evidence: `src/Libraries/Bolt/Bolt.Media/BoltMediaStream.cs:99-105`; `src/Libraries/Bolt/Bolt.Media.Browser/BoltMediaService.cs:183-230`; `src/Libraries/Bolt/Bolt.Browser/src/webcodecs-helper.ts:52-58,178-184`.

### M9. Hold does not stop media

The Hub considers `Held` media-active, and clients do not automatically pause capture or sending. Media continues after a hold signal, which is both a semantic and privacy defect.

Evidence: `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:923-924,1343-1365`.

### M10. Congestion control, simulcast, probing, and jitter are not integrated correctly

Controllers are attached to received rather than sender-owned streams, delay control mixes RTP ticks with milliseconds, probe feedback is not consumed, simulcast layer IDs are not assigned, and jitter buffers are not wired into received streams. Advertised adaptive behavior therefore does not operate end to end.

Evidence: `src/Libraries/Bolt/Bolt.Media/BoltMediaClient.cs:119-190`; `src/Libraries/Bolt/Bolt.Media/DelayBasedController.cs:68-84`; `src/Libraries/Bolt/Bolt.Media/MediaJitterBuffer.cs:47-100`; `src/Libraries/Bolt/Bolt.Browser/src/jitter-buffer.ts:58-143`.

### M11. QUIC/WebTransport datagrams and direct P2P are nonfunctional feature paths

No production caller selects a datagram transport, the transport contract lacks datagram receive, WebTransport negotiation returns `null`, direct P2P does not start the Bolt send loop, and no `/bolt-direct` server endpoint exists. The direct path also constructs plaintext `ws://` endpoints from remote data.

Evidence: `src/Libraries/Bolt/Bolt.Protocol/Transport/IBoltConnection.cs:25-32`; `src/Libraries/Bolt/Bolt.Client/Transport/BoltTransportNegotiator.cs:58-63`; `src/Libraries/Bolt/Bolt.Media/DirectConnectionManager.cs:119-160`.

### M12. WebSocket authorization outlives token expiration/revocation

The principal is captured at upgrade and never revalidated. Tokens without `exp` are accepted by shared JWT configuration. Expired, disabled, or stolen credentials remain active until the socket disconnects.

Evidence: `src/Kernel/XFramework.Core/Extensions/InstallerExtensions.cs:143-153`; `src/Libraries/Bolt/Bolt.Server/BoltServerExtensions.cs:96-110`; `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:216-228`.

### M13. Bolt-local service-discovery reads bypass the HTTP authorization policy

HTTP registry endpoints require a service/admin policy, but Bolt local commands only require an authenticated WebSocket. Handlers ignore `context.User`, use `CancellationToken.None`, and expose full/offline topology.

Evidence: `src/Modules/XFramework.Bolt/Bolt.Hub/Extensions/ApplicationBuilderExtension.cs:21-35`; `src/Modules/XFramework.Bolt/Bolt.Hub/Services/BoltServiceDiscoveryHostedService.cs:69-98`.

### M14. Discovery contracts and configured manifests disagree

Storage and Operations Dashboard configured manifest names do not equal their authenticated XFramework service names and are rejected on refresh. `MinVersion` is declared but ignored when evaluating dependency satisfaction.

Evidence: `src/Modules/XFramework.Bolt/Bolt.Hub/Services/BoltServiceDiscoveryRegistry.cs:393-488`; `src/Modules/XFramework.Storage/Storage.Api/appsettings.json:2-12`; `src/Presentation/XFramework.Operations.Dashboard/appsettings.json:9-20`.

### M15. Presence has no lease and inconsistent identity comparison

Online state does not expire by `LastSeenAt`, disconnect persistence errors are not retried, memory merges client IDs case-insensitively while PostgreSQL identity is case-sensitive, and stale records can continue satisfying dependencies.

Evidence: `src/Modules/XFramework.Bolt/Bolt.Hub/Services/BoltServicePresenceTracker.cs:7-33`; `src/Modules/XFramework.Bolt/Bolt.Hub/Services/BoltServiceDiscoveryRegistry.cs:165-223,349-360`.

### M16. Durable acknowledgement performs identity/database work per message

Communications authorization resolves JWT identity and queries credentials before switching on acknowledgement. A successful consume path can therefore require a database query per message and amplify database failure during replay.

Evidence: `src/Modules/XFramework.Bolt/Bolt.Hub/Services/CommunicationsBoltTopicAuthorizer.cs:33-65,149-180`; `src/Infrastructure/XFramework.Integration/Drivers/BoltDriver.cs:235-265`.

### M17. Staging Hub binds a different port than Compose publishes and probes

Staging Kestrel binds container port 7000, while Compose publishes and health-checks port 8080. Running the documented Compose staging command can leave Bolt Hub unreachable and unhealthy.

Evidence: `src/Modules/XFramework.Bolt/Bolt.Hub/appsettings.Staging.json:34-41`; `docker-compose.yml:3,13-15,97-120`.

## Low

### L1. Protocol and media documentation overstate current capabilities

The media README uses APIs on the wrong class, has stale test counts, and marks encryption/NACK/QUIC as both complete and missing. Performance docs publish numbers whose methodology no longer matches the current benchmark implementation.

Evidence: `src/Libraries/Bolt/BOLT-MEDIA.md`; `src/Libraries/Bolt/BOLT.md:29-47`.

### L2. Bolt-specific telemetry is absent

OpenTelemetry is installed, but no low-cardinality Bolt meter/activity source reports queue bytes, pending RPCs, route misses, frame rejects, send failures, replay backlog, command latency, or media drops.

Evidence: `src/Modules/XFramework.Bolt/Bolt.Hub/Program.cs:8-12`; frame processing in `src/Libraries/Bolt/Bolt.Server/BoltServer.cs`.

### L3. Browser/media validation is compile-heavy and integration-light

The browser package has no test script. There are no real-browser two-peer tests for WebCrypto, WebCodecs, timers, FEC/NACK/jitter, reconnect, answer-side setup, remote playback, or cleanup.

Evidence: `src/Libraries/Bolt/Bolt.Browser/package.json`; `src/Tests/Bolt.Tests/BoltMediaBrowserTests.cs`.

### L4. Several benchmark labels and thresholds are stale

Comments and assertions still describe different payload sizes, concurrency semantics, allocation targets, and zero-GC behavior than the current suite produces.

Evidence: `src/Tests/Bolt.Tests/BoltBenchmarks.cs`; `src/Libraries/Bolt/BOLT.md`.

### L5. Small ownership and validation gaps remain

`PooledMemoryOwner` claims pooled arrays are not GC-tracked, parser methods do not consistently validate frame discriminators, and WebTransport disposal does not explicitly close the session. Individually these are low severity but they obscure lifetime and protocol invariants.

Evidence: `src/Libraries/Bolt/Bolt.Protocol/Buffers/PooledMemoryOwner.cs`; codec parser and WebTransport implementation files.

## Validation Performed

### Correctness

- `dotnet test src/Tests/Bolt.Tests/Bolt.Tests.csproj -c Release`: **217 passed, 0 failed** in 4m21s.
- `npm exec --yes --package=typescript@5.7.3 -- tsc -- --noEmit` in `Bolt.Browser`: **passed**.
- The browser package exposes no runnable unit/integration test suite.

Passing tests do not cover the adversarial and end-to-end scenarios described above.

### Current Local Benchmark Results

Environment: Windows 11 under Hyper-V, .NET 10.0.0, Server GC, BenchmarkDotNet ShortRun, one launch, localhost/in-process/plaintext. CPU identification was unavailable. These numbers are suitable only as a local baseline.

| Scenario | Mean | Allocated |
|---|---:|---:|
| Bolt Hub, concurrency 1 | 136.47 us | 5.16 KB |
| Bolt Direct, concurrency 1 | 80.47 us | 3.37 KB |
| gRPC Hub, concurrency 1 | 287.31 us | 19.71 KB |
| gRPC Direct, concurrency 1 | 165.06 us | 8.64 KB |
| SignalR Hub, concurrency 1 | 153.53 us | 5.94 KB |
| Bolt Hub, 64-request batch | 1,039.95 us | 282.77 KB |
| Bolt Direct, 64-request batch | 1,761.96 us | 183.82 KB |
| gRPC Hub, 64-request batch | 1,368.63 us | 1,302.37 KB |
| gRPC Direct, 64-request batch | 1,561.77 us | 586.23 KB |

The current throughput suite reports approximately 88,087 Bolt Hub, 70,838 Bolt Direct, 45,930 gRPC Hub, 55,491 gRPC Direct, and 11,542 SignalR operations/second. Treat these as batch-amortized harness values until H22 is fixed.

Benchmark artifacts:

- `src/Tests/Bolt.Tests/BenchmarkDotNet.Artifacts/results/Bolt.Tests.BoltBenchmarks-report-github.md`
- `src/Tests/Bolt.Tests/BenchmarkDotNet.Artifacts/results/Bolt.Tests.BoltThroughputBenchmarks-report-github.md`

## Required Performance Gate

Do not publish a universal "faster than gRPC" claim until all of the following are measured from the same workload and security posture:

1. Equivalent typed and raw unary payloads from 0 B through 100 MB, including serialization/deserialization and status validation.
2. Equal channel/connection sweeps plus each protocol's documented best configuration.
3. gRPC channel reuse, multiple HTTP/2 connections where saturated, tuned connection/stream flow-control windows, and bidirectional streaming.
4. TLS 1.3 and mTLS, same-host, cross-host, 1/10/50/100 ms RTT, bandwidth limits, packet loss, reconnect, and rolling deployment.
5. Closed-loop and open-loop load with request-level HDR p50/p95/p99/p99.9, coordinated-omission correction, errors, queue time, CPU, context switches, copy bytes, working set, pool retention, and GC pause time.
6. Cold start, connection churn, 1/10/100/1,000/10,000 clients, large fanout, slow consumers, soak, and failure injection.
7. A CI regression gate based on a stable dedicated runner and statistically meaningful runs, not one ShortRun launch.

Relevant comparison standards:

- gRPC for .NET performance guidance: <https://learn.microsoft.com/en-us/aspnet/core/grpc/performance?view=aspnetcore-10.0>
- gRPC performance best practices: <https://grpc.io/docs/guides/performance/>
- gRPC benchmarking methodology: <https://grpc.io/docs/guides/benchmarking/>

## Remediation Order

1. **Containment:** enforce reserved identity binding, remove shared-key token minting, require WSS/mTLS, lower frame ceilings, and apply byte/rate/concurrency quotas.
2. **Correctness and ownership:** fix pooled RPC generation ownership, custom-frame lifetime, large-response affinity, send completion/error propagation, and connection/stream cleanup.
3. **Media correctness/security:** bound sequence/NACK behavior, repair FEC, make encryption fail closed and authenticated, fix browser stream wiring, and implement group membership/key epochs.
4. **Memory/performance architecture:** byte-bounded queues, explicit pooled ownership, fewer full-frame copies, smaller/shorter-lived receive buffers, bounded dispatch, and compact negotiated identifiers.
5. **Hub scalability:** default-deny pub/sub, durable subscriber leases, service-registry retention/snapshots, token-lifetime handling, and an inter-node routing/presence design.
6. **Proof:** replace the comparison harness, add adversarial and real-browser media tests, establish protocol telemetry, then set workload-specific performance targets against tuned alternatives.

## Findings Explicitly Not Confirmed

- Thread-local writer corruption was considered but not accepted as a finding: current send paths snapshot writer memory into a queue-owned buffer before the writer can be reused. Lifetime bugs still exist elsewhere, as documented above.
- The XFramework Bolt Hub endpoint itself is not anonymous; it calls `RequireAuthorization`. Generic `BoltServer` can be hosted without auth, but Hub findings are scoped to authenticated attackers unless stated otherwise.
- A passing primitive/unit test is not evidence that high-level Bolt Media works end to end; no two-browser media path is exercised.
