---
title: Bolt Protocol, Hub, and Media Investigation
date: 2026-07-19
category: workflow-issues
module: Bolt
problem_type: audit
severity: high
status: complete
tags:
  - bolt
  - protocol
  - hub
  - media
  - performance
  - reliability
---

# Bolt Protocol, Hub, and Media Investigation

## Scope

This investigation follows the scope and priorities in
`bolt-protocol-hub-media-remediation-plan-2026-07-12.md`.

It covers:

- Bolt wire protocol, codec, client, and server behavior.
- Bolt Hub routing, queues, authorization, discovery, and observability.
- Benchmark validity and the evidence behind performance claims.
- Bolt Media readiness as a separate, currently disabled feature.

It does not propose Bolt-managed TLS, Tailscale ACL changes, Docker certificate
management, deployment topology changes, or unrelated application work.

The review used four parallel investigation tracks:

1. Protocol and client correctness, memory ownership, and backpressure.
2. Hub routing, queues, authorization, discovery, and health.
3. Benchmark methodology and public performance claims.
4. Media signaling, transport, FEC, encryption, browser support, and tests.

Baseline: `develop` at commit `c4682cb1dfb94f4e22e983fcd1ccb6981c41c4b9`.

## Executive Assessment

No current critical vulnerability or data-loss defect was confirmed.

The core Bolt path has several real high-severity reliability and memory
problems that should be fixed before claiming it is production-ready at very
high concurrency. The largest risks are client-side buffer ownership, send
completion semantics, stream backpressure, connection affinity for large
responses, and Hub queues that are bounded by item count but not total bytes.

The current benchmark suite is useful for development, but it does not yet
support the README claims that Bolt is faster and substantially more
memory-efficient than gRPC. Several comparisons use different APIs, topologies,
connection counts, or latency definitions.

Bolt Media is correctly disabled in the XFramework applications. It has
multiple enablement blockers and should remain off until a basic two-browser
media path is correct and covered. Media issues do not block the normal Bolt
RPC, event, or Hub paths while Media remains disabled.

Related audit items are consolidated below so that the remediation stays
focused.

| Severity | Core findings | Assessment |
|---|---:|---|
| Critical | 0 | None confirmed |
| High | 9 | Correctness, memory, backpressure, and benchmark validity |
| Medium | 7 | Validation, discovery, routing, configuration, and efficiency |
| Low | 3 | Observability, shutdown behavior, and test reliability |
| Media enablement blockers | 5 | High impact only if Media is enabled |

## High-Severity Findings

### H1. Client custom-frame buffers are not consistently returned

**Evidence**

- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:752`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1237`
- `src/Libraries/Bolt/Bolt.Media/BoltMediaClient.cs:141`

The client receive path rents a buffer and transfers it to a custom-frame
handler. Built-in event handling copies the payload but does not return the
original rented frame buffer. Sustained event or custom-frame traffic can
therefore create continuing `ArrayPool` pressure and more garbage collections.

**Recommended action**

Define one explicit ownership contract for custom frames. Prefer a scoped
pooled owner that is disposed by the dispatcher, with handlers copying only
when they need to retain data beyond the callback.

### H2. Large RPC responses can use two different connections

**Evidence**

- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:292`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:298`

The large-response path creates a `BoltStream` with one call to
`GetConnection()`, then sends the stream-open frame through another call. With
multiple active connections, the stream registration and its frames can land
on different connections.

**Impact**

Large responses can fail intermittently under connection pooling even though
ordinary responses work.

**Recommended action**

Select one connection once and use it for stream registration, stream-open,
all stream frames, and cleanup.

### H3. Client send completion does not mean transport completion

**Evidence**

- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1718`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1724`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1741`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1767`

`SendAsync` completes after queue admission. If the transport is disconnected,
the send loop can skip the actual send. Send-loop exceptions notify an
observer, but do not complete the originating operation.

**Impact**

- RPC calls wait until timeout instead of failing promptly.
- Push, publish, and stream operations can appear successful when no frame was
  transmitted.
- Dead connections can remain eligible for later operations.

The equivalent Hub send-failure path has already been hardened; the client path
still needs the same reliable completion and connection retirement behavior.

**Recommended action**

Complete each queued send only after the transport accepts it. Fail its
completion source on disconnect or send error, and retire the connection from
selection immediately.

### H4. An unread stream can block all traffic on its connection

**Evidence**

- `src/Libraries/Bolt/Bolt.Client/BoltStream.cs:42`
- `src/Libraries/Bolt/Bolt.Client/BoltStream.cs:166`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1227`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1329`

Each stream uses a bounded channel with wait-on-full behavior. The shared
connection receive loop awaits writes to that channel. If a consumer stops
reading, the receive loop eventually blocks and prevents unrelated RPC,
event, and stream frames on the same connection from being processed.

**Recommended action**

Never wait indefinitely for one stream from the shared receive loop. On stream
overflow, fail or reset that stream and allow the connection to continue.

### H5. Client inbound dispatch and unknown streams lack firm admission limits

**Evidence**

- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1213`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1220`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1313`

Inbound requests and pushes rent full-frame buffers and launch asynchronous
work without a bounded dispatcher. Stream state can also be added before a
matching handler is confirmed, leaving unknown streams retained.

**Impact**

A burst of valid or unwanted inbound traffic can produce excessive concurrent
tasks and pooled-buffer retention.

**Recommended action**

Add a bounded client dispatch budget, reject work before allocating large
payload buffers where possible, and do not retain stream state until a valid
handler or pending operation is found.

### H6. Local timeouts do not cancel remote work

**Evidence**

- `src/Libraries/Bolt/Bolt.Protocol/Protocol/BoltCodec.cs:48`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:544`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1372`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:629`

Bolt has local timeout and cancellation handling but no protocol-level request
deadline or cancellation frame. Once a request is sent, a client timeout does
not tell the server to stop the associated work.

**Impact**

During overload or downstream latency, callers can abandon requests while the
server continues consuming CPU, database connections, and memory. This reduces
throughput precisely when the system is under pressure.

**Recommended action**

Add a compact optional deadline to request metadata and a cancellation control
frame keyed by request ID. Keep compatibility by making both optional.

### H7. The client receive path retains and copies more memory than necessary

**Evidence**

- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1131`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1154`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1296`
- `src/Libraries/Bolt/Bolt.Client/BoltClient.cs:1767`
- `src/Libraries/Bolt/Bolt.Protocol/Buffers/PooledMemoryOwner.cs:51`

Each connection allocates a large receive buffer. Fragment assembly can retain
up to the configured frame limit until another fragmented frame or disconnect.
Some paths perform additional full-payload copies, and pooled memory owners
have a finalizer-backed fallback.

**Impact**

At high connection counts or with large fragmented messages, retained memory
and copy cost can dominate otherwise efficient framing.

**Recommended action**

Release fragment buffers immediately after dispatch, make pooled ownership
deterministic, and measure whether the fixed per-connection buffer can be
reduced without lowering throughput.

### H8. Hub queues are bounded by item count, not retained bytes

**Evidence**

- `src/Libraries/Bolt/Bolt.Server/BoltServerExtensions.cs:19`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:3677`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:3697`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:3822`
- `src/Libraries/Bolt/Bolt.Server/Durable/DurableQueueOptions.cs:14`

Hub send and durable queues enforce item capacities, but frames may be as large
as 8 MiB. Buffers are allocated or copied before queue admission in important
paths. A queue can therefore retain a very large amount of memory while still
being within its item limit.

**Recommended action**

Add a byte budget alongside the existing item count for send, durable, and
replay queues. Reserve budget before copying payloads and release it when an
item completes or is dropped.

### H9. Current benchmarks do not substantiate public gRPC claims

**Evidence**

- `README.md:3`
- `README.md:68`
- `README.md:97`
- `src/Tests/Bolt.Tests/BoltBenchmarks.cs:92`
- `src/Tests/Bolt.Tests/BoltBenchmarks.cs:161`
- `src/Tests/Bolt.Tests/BoltBenchmarks.cs:241`
- `src/Tests/Bolt.Tests/BoltBenchmarks.cs:272`
- `src/Tests/Bolt.Tests/PayloadBenchmarks.cs:65`
- `src/Tests/Bolt.Tests/PayloadBenchmarks.cs:92`
- `src/Tests/Bolt.Tests/PayloadBenchmarks.cs:459`
- `src/Tests/Bolt.Tests/ScalabilityBenchmarks.cs:39`
- `src/Tests/Bolt.Tests/ScalabilityBenchmarks.cs:150`

The README states specific speed, memory, zero-copy, and zero-GC advantages.
The available comparisons do not consistently measure equivalent work:

- Some compare raw Bolt payload handling with typed gRPC serialization.
- Some compare a Hub route with a direct gRPC topology.
- Batch `Task.WhenAll` duration is presented as latency rather than measuring
  per-request latency distribution.
- Client, server, and Hub can run in the benchmark process, which makes
  `MemoryDiagnoser` attribution incomplete.
- Connection counts and gRPC tuning are not consistently equivalent.
- Several benchmarks do not validate every response before recording results.

**Recommended action**

Qualify or remove unsupported numerical claims now. Establish equivalent
direct and Hub unary workloads first, validate responses, measure throughput
and p50/p95/p99 latency, and collect per-process allocations and working set.
Only optimize bottlenecks demonstrated by those measurements.

## Medium-Severity Findings

### M1. Route registration and disconnect can race

**Evidence**

- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:614`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:3030`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:3122`

Route registration appends to a concurrent collection while disconnect cleanup
can replace the collection. A registration racing with replacement can be
lost. Recipient selection also scans registrations.

**Recommended action**

Use one atomic mutation strategy per route key and preserve the simplest
collection that meets measured routing scale.

### M2. Codec writers can emit values that readers reject

**Evidence**

- `src/Libraries/Bolt/Bolt.Protocol/Protocol/BoltCodec.cs:146`
- `src/Libraries/Bolt/Bolt.Protocol/Protocol/BoltCodec.cs:211`
- `src/Libraries/Bolt/Bolt.Protocol/Protocol/BoltCodec.cs:443`
- `src/Libraries/Bolt/Bolt.Protocol/Protocol/BoltCodec.cs:961`

Some writer paths do not enforce the same limits as their corresponding
readers. NACK entry counts can also narrow to `ushort` while additional entries
remain in the frame.

**Recommended action**

Centralize shared limits and make writers reject data that cannot be read
unambiguously.

### M3. Browser codec parsing performs avoidable copies and weak validation

**Evidence**

- `src/Libraries/Bolt/Bolt.Browser/src/protocol.ts:226`
- `src/Libraries/Bolt/Bolt.Browser/src/protocol.ts:562`
- `src/Libraries/Bolt/Bolt.Browser/src/protocol.ts:704`
- `src/Libraries/Bolt/Bolt.Browser/src/protocol.ts:878`

Browser parsing uses slicing in hot paths, does not consistently enforce the
8 MiB frame ceiling, and accepts malformed UUID hex by mapping invalid values
to zero. Numeric conversion also needs explicit safe-integer checks before
conversion to `BigInt`.

**Recommended action**

Use views instead of copies where lifetime is clear and apply the same frame,
UUID, and integer validation as the .NET codec.

### M4. Durable ACK authorization repeats expensive work

**Evidence**

- `src/Modules/XFramework.Bolt/Bolt.Hub/Services/CommunicationsBoltTopicAuthorizer.cs:58`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:2672`

Durable acknowledgements can repeat token and database authorization work for
the same established connection and subscription.

**Recommended action**

Cache the validated subscription authorization for the connection lifetime and
invalidate it on unsubscribe, disconnect, or credential expiry.

### M5. Discovery and presence records are not bounded for long-lived Hubs

**Evidence**

- `src/Modules/XFramework.Bolt/Bolt.Hub/Services/BoltServiceDiscoveryRegistry.cs:123`
- `src/Modules/XFramework.Bolt/Bolt.Hub/Services/BoltServiceDiscoveryRegistry.cs:317`
- `src/Modules/XFramework.Bolt/Bolt.Hub/Services/BoltServiceDiscoveryRegistry.cs:349`

Registrations are retained as records and offline instances are repeatedly
rebuilt into responses. There is no presence lease or simple retirement rule.
This is not an immediate capacity issue at the current service count, but it
creates avoidable growth in a long-running Hub.

**Recommended action**

Retire offline records after a conservative retention period and avoid
rebuilding unchanged discovery results.

### M6. Two checked-in service manifests do not match authenticated names

**Evidence**

- `src/Modules/XFramework.Storage/Storage.Api/appsettings.json:12`
- `src/Presentation/XFramework.Operations.Dashboard/appsettings.json:20`
- `src/Modules/XFramework.Bolt/Bolt.Hub/Services/BoltServiceDiscoveryRegistry.cs:481`

The manifests use `Storage` and `OperationsDashboard`, while discovery now
requires the manifest service name to match the authenticated XFramework
service identity. These registrations will be rejected when the names differ.

**Recommended action**

Align the two manifest names with their issued service identities and add one
configuration test that checks all registered manifests.

### M7. Generic Bolt Server still enables Media by default

**Evidence**

- `src/Libraries/Bolt/Bolt.Server/BoltServerExtensions.cs:34`

XFramework application settings and Docker configuration explicitly disable
Media, but the reusable server option defaults it to enabled. That conflicts
with the current quarantine policy and makes accidental enablement possible in
a new host.

**Recommended action**

Change the reusable default to `false`; require explicit opt-in by a host that
has passed the Media enablement tests.

## Low-Severity Findings

### L1. Transport telemetry does not cover the main performance risks

**Evidence**

- `src/Libraries/Bolt/Bolt.Server/BoltServerMetrics.cs:6`
- `src/Modules/XFramework.Bolt/Bolt.Hub/Health/BoltTransportHealthCheck.cs:30`

Current metrics cover selected registration, quota, plaintext, oversized-frame,
and disabled-Media events. They do not expose route misses, command latency,
client send failures, replay backlog, retained pooled bytes, or receive-loop
state.

**Recommended action**

Add only the counters and histograms needed to validate the fixes and
benchmarks. Avoid turning health checks into a full metrics system.

### L2. Discovery shutdown ignores cancellation in several waits

**Evidence**

- `src/Modules/XFramework.Bolt/Bolt.Hub/Services/BoltServiceDiscoveryHostedService.cs:65`
- `src/Modules/XFramework.Bolt/Bolt.Hub/Services/BoltServiceDiscoveryHostedService.cs:90`
- `src/Modules/XFramework.Bolt/Bolt.Hub/Services/BoltServiceDiscoveryHostedService.cs:115`

Several discovery operations use `CancellationToken.None`, which can delay a
clean shutdown.

**Recommended action**

Flow the hosted-service stopping token through those operations.

### L3. One containment test is timing-sensitive

**Evidence**

- `src/Tests/Bolt.Tests/BoltPhase0ContainmentTests.cs:53`

`MediaDisabled_RejectsCallSignalWithoutCreatingResponseTraffic` failed once in
five isolated runs because teardown can occur before the queued registration
acknowledgement is observed. This is a test synchronization issue, not evidence
that the disabled-Media production guard is broken.

**Recommended action**

Wait explicitly for registration completion before sending the Media frame and
asserting the absence of response traffic.

## Media Enablement Blockers

These findings are high impact if Media is enabled. They are listed separately
because XFramework currently disables Media and the Hub rejects Media frames.

### ME1. Sequence and NACK handling can amplify malformed ranges

**Evidence**

- `src/Libraries/Bolt/Bolt.Media/NackTracker.cs:70`
- `src/Libraries/Bolt/Bolt.Media/BoltMediaStream.cs:384`
- `src/Libraries/Bolt/Bolt.Browser/src/media-stream.ts:135`
- `src/Libraries/Bolt/Bolt.Protocol/Protocol/BoltCodec.cs:788`

Sequence wraparound and NACK range handling are inconsistent between paths.
Malformed or very large ranges can trigger excessive retransmission work.

### ME2. FEC grouping is incomplete and can retain state

**Evidence**

- `src/Libraries/Bolt/Bolt.Media/BoltMediaStream.cs:390`
- `src/Libraries/Bolt/Bolt.Media/BoltMediaStream.cs:415`
- `src/Libraries/Bolt/Bolt.Media/FecDecoder.cs:5`
- `src/Libraries/Bolt/Bolt.Browser/src/media-stream.ts:275`

The current parity grouping and recovery assumptions do not reliably identify
the protected packet set, and decoder state does not have a firm lifetime
bound.

### ME3. The Blazor Media surface is not an end-to-end media path

**Evidence**

- `src/Libraries/Bolt/Bolt.Media.Browser/BoltMediaService.cs:200`
- `src/Libraries/Bolt/Bolt.Media.Browser/BoltMediaService.cs:247`
- `src/Libraries/Bolt/Bolt.Media/BoltMediaClient.cs:116`

The service APIs do not yet provide a complete capture, send, receive, decode,
and playback workflow.

### ME4. Media encryption can fail open and lacks authenticated framing

**Evidence**

- `src/Libraries/Bolt/Bolt.Media/BoltMediaStream.cs:260`
- `src/Libraries/Bolt/Bolt.Media/BoltMediaStream.cs:356`
- `src/Libraries/Bolt/Bolt.Media/BoltMediaClient.cs:257`

Encryption setup can fall back to unencrypted media, and encrypted payloads do
not have a complete authenticated framing contract. Media must not be enabled
for sensitive traffic in this state.

### ME5. Hold signaling does not stop media transmission

**Evidence**

- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:1403`
- `src/Libraries/Bolt/Bolt.Server/BoltServer.cs:1876`

Call hold state is signaled but not enforced against subsequent media frames.

**Media recommendation**

Keep Media disabled. When the project resumes, first make one two-browser audio
or video call path work with strict bounds, authenticated encryption, hold, and
cleanup. Group calls, processor pipelines, adaptive quality, and alternate
transports should remain deferred until that basic path is correct and
measured.

## Confirmed Improvements Already Present

The current branch already contains important protections from the earlier
remediation work:

- Production reserved service identities are bound to authenticated identities.
- Transport tokens are short-lived, centrally issued by IdentityServer, and
  signed with RS256; application services do not hold the signing key.
- Pending RPC completion removes the exact pending entry before reuse,
  preventing pooled-operation ABA completion.
- Registration has a deadline and cleanup path.
- Hub send failures complete callers and retire failed connections.
- Hub inbound quotas are wired.
- Unknown durable topics are denied by default, subscriber identifiers are
  validated, and durable subscription counts are capped.
- Topic authorization checks tenant, credential, soft-delete, and membership
  state, including per-frame denial where required.
- Token expiry is mandatory and connection lifetime is bounded by expiry.
- Service-token acquisition is single-flight and bounded.
- The maximum frame size was reduced from 100 MiB to 8 MiB.
- Media is disabled in XFramework application and Docker configuration.

These controls should be preserved while addressing the remaining findings.

## Verification Performed

### .NET tests

`dotnet test src/Tests/Bolt.Tests/Bolt.Tests.csproj -c Release --nologo`

- 373 total
- 364 passed
- 4 failed because PostgreSQL Testcontainers/Docker was unavailable and no
  `BOLT_TEST_POSTGRES_CONNECTION` was configured
- 5 Redis durable tests skipped because the required environment was unavailable

Excluding PostgreSQL-dependent tests:

- 369 total
- 363 passed
- 1 timing-sensitive containment test failed
- 5 Redis tests skipped

The timing-sensitive test passed four of five isolated runs. Focused Media
tests passed 67 tests.

### Browser protocol

TypeScript compilation completed successfully with TypeScript 5.7.3:

`npm exec --yes --package=typescript@5.7.3 -- tsc -- --noEmit`

### Remaining validation gaps

- PostgreSQL authorization tests need Docker/Testcontainers or an explicit test
  connection.
- Redis durable replay tests need their integration environment.
- Media has no real two-browser end-to-end test and remains disabled.
- Competitive benchmark results need to be regenerated after methodology is
  corrected.

## Focused Remediation Order

### Phase 1: Protocol and client correctness

1. Fix custom-frame buffer ownership and immediate fragment-buffer release.
2. Make client send completion reliable and retire failed connections.
3. Pin large-response streams to one connection.
4. Prevent unread streams and inbound dispatch from blocking or exhausting a
   connection.
5. Add optional remote deadline/cancellation support.
6. Align .NET and browser writer/reader validation.

After each group, run focused tests and a reviewer pass against this report.

### Phase 2: Hub bounds and efficiency

1. Add byte budgets to send, durable, and replay queues.
2. Make route registration/disconnect mutation atomic.
3. Avoid repeated durable-ACK authorization work.
4. Correct the two service manifest names.
5. Bound stale discovery records and add focused transport metrics.

### Phase 3: Benchmark and optimize

1. Qualify unsupported README claims immediately.
2. Build equivalent direct Bolt and gRPC unary benchmarks.
3. Add an equivalent Hub topology comparison.
4. Measure validated throughput, p50/p95/p99 latency, allocations, and
   per-process memory.
5. Optimize only bottlenecks demonstrated by those results.

### Separate Media track

1. Change the generic server default to Media disabled.
2. Keep Media off in XFramework.
3. Resume only with a scoped two-browser call milestone.
4. Address sequence/NACK, FEC, authenticated encryption, hold, and cleanup
   before broader Media features.

## Conclusion

Bolt has a compact architecture and several solid safety improvements already
in place, but the current evidence does not yet support positioning it as
faster or more memory-efficient than gRPC in general. The most valuable next
work is not broader infrastructure: it is fixing the concrete client memory,
send, stream, and Hub byte-budget issues, then measuring equivalent workloads.

That sequence keeps the remediation aligned with the stated goal while
avoiding unrelated networking or deployment complexity.
