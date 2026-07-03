---
title: "Bolt SignalR Removal and Service-to-Service Migration"
date: 2026-04-07
category: architecture-patterns
module: Bolt
problem_type: architecture_pattern
component: service_object
severity: critical
applies_when:
  - "Replacing SignalR transport with Bolt binary WebSocket frames and native pub/sub while preserving IMessageBusWrapper consumers"
tags: [bolt, signalr, transport, pubsub, migration]
---

# Bolt: SignalR Removal & Service-to-Service Migration

**Date:** 2026-04-07
**Status:** Approved
**Scope:** Replace SignalR transport with Bolt thin protocol for service-to-service RPC and pub/sub. Remove SignalR entirely from client and Hub.

## Goal

Migrate all XFramework services from SignalR-based transport (`BoltDriverSignalR` -> `SignalRService` -> `HubConnection`) to the existing Bolt thin protocol (`BoltDriver` -> `BoltClient` -> WebSocket binary frames). Add native pub/sub support to the Bolt protocol. Delete all SignalR code from both client (`XFramework.Integration`) and Hub (`Bolt.Hub`).

The `IMessageBusWrapper` interface stays unchanged so all service code continues to work without modification.

## Non-Goals

- **DB proxy decentralization** - Moving `ExecuteQuery`/`ExecuteChanges` from Hub into individual services is parked as a separate brainstorm. The Hub keeps temporary `ExecuteQuery`/`ExecuteChanges` Bolt frame handlers as a transitional shim so existing `RemoteDataContext` clients keep working.
- **Wildcard topic patterns** (e.g., `chat.room.*`) - V1 uses exact-match topics only.
- **Migration of any code outside the `IMessageBusWrapper` consumers** - The interface stays identical.

## Architecture

### Before

```
Service Code
    v IMessageBusWrapper
BoltDriverSignalR  (XFramework.Integration)
    v ISignalRService
SignalRService     (XFramework.Integration)
    v HubConnection (Microsoft.AspNetCore.SignalR.Client)
SignalR Hub        (/stream-flow/queue on Bolt.Hub)
    v MessageQueueHub
IBoltHubService -> service routing
```

### After

```
Service Code
    v IMessageBusWrapper        (unchanged)
BoltDriver         (XFramework.Integration - renamed, internals replaced)
    v BoltClient
BoltClient         (Bolt.Client library - already exists)
    v WebSocket binary frames
BoltServer         (/bolt/ws on Bolt.Hub - already exists)
    v frame routing
IBoltHubService -> service routing
```

## Pub/Sub Protocol Extensions

### New Frame Types

Extend `FrameType` enum in `Bolt.Protocol`:

| Frame | Hex | Direction | Purpose |
|---|---|---|---|
| `Subscribe` | 0x10 | Client -> Hub | Subscribe to a topic (transient or durable) |
| `Unsubscribe` | 0x11 | Client -> Hub | Unsubscribe from a topic |
| `Publish` | 0x12 | Client -> Hub | Publish to a topic (Hub fans out + queues for durable subscribers) |
| `Event` | 0x13 | Hub -> Client | Deliver published message to subscriber (carries sequence number for durable) |
| `Ack` | 0x14 | Client -> Hub | Acknowledge processing of a durable Event up to a sequence number |
| `ExecuteQuery` | 0x15 | Client -> Hub | Temporary DB proxy shim (parked work) |
| `ExecuteChanges` | 0x16 | Client -> Hub | Temporary DB proxy shim (parked work) |

### Frame Layouts

All multi-byte integers little-endian. Topic hash is FNV1a of the UTF-8 topic string.

```
Subscribe    [1:type=0x10] [4:topicHash] [1:flags] [4:subscriberIdLen] [subscriberId UTF-8] [4:topicLen] [topic UTF-8]
Unsubscribe  [1:type=0x11] [4:topicHash] [4:subscriberIdLen] [subscriberId UTF-8]
Publish      [1:type=0x12] [4:topicHash] [1:flags] [4:payloadLen] [payload]
Event        [1:type=0x13] [4:topicHash] [8:sequenceNumber] [1:flags] [4:payloadLen] [payload]
Ack          [1:type=0x14] [4:topicHash] [4:subscriberIdLen] [subscriberId UTF-8] [8:upToSequenceNumber]
```

**Subscribe flags:** `0x01` = durable. When set, the Hub persists messages for this subscriber and replays unacked messages on reconnect. When unset, the subscription is transient (in-memory only, no persistence, no replay).

**Publish flags:** `0x01` = durable-eligible. When set, the Hub queues the message for any durable subscribers on this topic. When unset, the message is fan-out only (no queuing even for durable subscribers - useful for ephemeral signals like typing indicators).

**Event flags:** `0x01` = replay (delivered from durable queue, not live). Lets clients distinguish replay traffic from live traffic if they care.

**Sequence number:** Monotonically increasing per `(topicHash, subscriberId)`. The Hub assigns sequence numbers when queuing a message. Live (non-queued) deliveries use sequence `0`.

**SubscriberId vs ClientId:** `clientId` is per-connection (changes on reconnect). `subscriberId` is stable across reconnects - typically the service's name or a persistent UUID. Durable subscriptions are keyed by `(topicHash, subscriberId)` so reconnecting clients resume from where they left off.

### Pub/Sub Semantics

- **Topic hashing:** FNV1a of the topic string. Hub keys subscriptions by hash for O(1) lookup. Same pattern as RPC routing by service hash.
- **Publish does not echo:** A publisher does not receive its own published messages. Standard pub/sub semantics (matches NATS, Redis pub/sub). Services that want to consume their own events should call the local handler directly.
- **No wildcards:** Exact-match topic strings only.
- **Reconnection:** When `BoltClient` reconnects after a network drop, it automatically re-sends all active `Subscribe` frames. Subscriber `Channel<T>` instances persist across reconnects so consumers see no interruption.
- **Two subscription modes:**
  - **Transient (default):** No persistence. Offline subscribers miss messages. Suitable for presence, typing indicators, ephemeral signals.
  - **Durable (opt-in):** Hub queues messages while subscriber is offline. On reconnect, the Hub replays all queued messages, then resumes live delivery. Suitable for chat messages, notifications, anything where missing a message is unacceptable.

### Durable Subscriptions

**Storage backend:** Redis (preferred) with in-memory fallback.

- If `BoltConfiguration.Redis.ConnectionString` is set, the Hub uses Redis for durable queues.
- If not set, the Hub falls back to in-memory storage with a warning at startup. In-memory mode loses queued messages on Hub restart and does not support multiple Hub instances.
- Redis backend uses Redis Streams (`XADD`/`XREAD`) per `(topicHash, subscriberId)`. Streams provide native sequence numbers, range queries, and trim operations.
- Stream key format: `bolt:durable:{topicHash}:{subscriberId}`.

**Delivery semantics: at-least-once with explicit ack**

1. Client subscribes with `durable=true` and a stable `subscriberId` (e.g., service name).
2. On `Publish` with `durable-eligible=true`, the Hub:
   - Fans out live `Event` frames to currently-connected subscribers (with `sequence=0`, no ack required).
   - For each registered durable subscriber on the topic, appends the payload to that subscriber's Redis stream and gets back a sequence number.
   - For currently-connected durable subscribers, sends the live `Event` frame with the assigned sequence number.
3. Client processes the message and sends an `Ack` frame with `upToSequenceNumber`.
4. Hub trims the stream up to and including the acked sequence number.
5. If the client crashes before acking, the next reconnect re-delivers all unacked messages from the stream (with `flags=replay`).

**Replay on reconnect:**

1. Client reconnects and sends `Subscribe(topic, durable=true, subscriberId)`.
2. Hub looks up the stream for `(topicHash, subscriberId)`.
3. If unacked messages exist, Hub sends them as `Event` frames with `flags=replay` and their original sequence numbers.
4. After the replay batch completes, the Hub resumes live delivery.
5. Live messages published during replay are queued in the stream and delivered after the replay batch finishes. This preserves ordering: replay then live.

**Retention policy (configurable per Hub):**

- `BoltConfiguration.Durable.MessageTtlSeconds` - Default 604800 (7 days). Redis stream entries older than this are trimmed by a periodic background job.
- `BoltConfiguration.Durable.MaxQueueSize` - Default 10000 messages per subscriber. When exceeded, the oldest messages are dropped (`XADD MAXLEN ~ 10000`).
- `BoltConfiguration.Durable.MaxReplayBatchSize` - Default 1000 messages per reconnect. Larger queues are replayed in chunks.

**Subscriber identity:**

- `subscriberId` is a stable string the application chooses. Examples:
  - Service-to-service: use the service name (e.g., `"XFramework.IdentityServer"`).
  - User-facing chat: use the user GUID.
  - Anonymous browser session: persist a UUID in `localStorage`.
- The Hub does not authenticate `subscriberId` - it's the application's responsibility to ensure subscribers don't impersonate each other. (Authentication is a separate layer.)

**Failure modes:**

- **Client never acks:** Stream grows until `MaxQueueSize` then drops oldest. Application must process and ack regularly.
- **Hub restarts (Redis backend):** Stream survives in Redis. Reconnecting clients resume normally.
- **Hub restarts (in-memory backend):** All queued messages lost. Subscribers see no replay. Logged as warning at Hub shutdown.
- **Redis unreachable:** Hub logs error and falls back to fan-out-only delivery for new publishes (effectively transient mode for all subscribers until Redis recovers). Existing live subscribers continue to receive messages.

### Hub-Side Routing

`BoltServer` adds state for both transient and durable subscriptions:

```csharp
// Transient subscriptions: live fan-out only
private readonly ConcurrentDictionary<int, ConcurrentBag<SubscriberConnection>> _liveSubscribersByTopic = new();
private readonly ConcurrentDictionary<BoltConnection, HashSet<int>> _liveSubscriptionsByConnection = new();

// Durable subscriptions: persistent identity (stable across reconnects)
// Maps topicHash -> set of subscriberIds that have ever registered as durable for this topic
private readonly ConcurrentDictionary<int, HashSet<string>> _durableSubscribersByTopic = new();

// Maps (topicHash, subscriberId) -> currently-connected connection (if any)
private readonly ConcurrentDictionary<(int TopicHash, string SubscriberId), BoltConnection> _liveDurableConnections = new();

// Per-Hub durable storage abstraction (Redis or in-memory)
private readonly IDurableQueueStore _durableStore;
```

`SubscriberConnection` carries the connection plus an optional `subscriberId` (for durable subs).

`IDurableQueueStore` interface (Hub-internal):

```csharp
public interface IDurableQueueStore
{
    Task<long> AppendAsync(int topicHash, string subscriberId, ReadOnlyMemory<byte> payload, CancellationToken ct);
    IAsyncEnumerable<(long Sequence, byte[] Payload)> ReadFromAsync(int topicHash, string subscriberId, long fromSequence, int maxCount, CancellationToken ct);
    Task AckAsync(int topicHash, string subscriberId, long upToSequence, CancellationToken ct);
    Task RegisterDurableSubscriberAsync(int topicHash, string subscriberId, CancellationToken ct);
}
```

Two implementations:
- `RedisDurableQueueStore` - uses Redis Streams
- `InMemoryDurableQueueStore` - uses `ConcurrentDictionary<(int, string), List<(long, byte[])>>` with locking; logs warning on Hub startup

Frame handling:

- **Subscribe (transient):** Add connection to `_liveSubscribersByTopic[topicHash]` bag, add topic hash to `_liveSubscriptionsByConnection[connection]` set, send ack.
- **Subscribe (durable):** Same as transient, plus:
  - Call `_durableStore.RegisterDurableSubscriberAsync(topicHash, subscriberId)` to ensure the subscriber is known.
  - Set `_liveDurableConnections[(topicHash, subscriberId)] = connection`.
  - Look up unacked messages via `_durableStore.ReadFromAsync(...)`. Send each as an `Event` frame with `flags=replay` and the stored sequence number, in batches up to `MaxReplayBatchSize`.
  - Send ack frame to signal end of replay.
- **Unsubscribe:** Remove from `_liveSubscribersByTopic`. For durable, remove from `_liveDurableConnections` (does NOT delete the queue - durable subscribers can disconnect and reconnect). To permanently delete a durable subscription, the application must explicitly call a separate `DeleteDurableSubscriptionAsync` method (not exposed via frame in V1; admin operation).
- **Publish:**
  - Fan out live `Event` frames to all entries in `_liveSubscribersByTopic[topicHash]` (skip publisher).
  - If `flags=durable-eligible`, for each subscriberId in `_durableSubscribersByTopic[topicHash]`:
    - Append to durable store, get sequence number.
    - If `_liveDurableConnections[(topicHash, subscriberId)]` exists, send live `Event` with the assigned sequence number (overrides the live fan-out for that subscriber to avoid duplicates).
- **Ack:** Call `_durableStore.AckAsync(topicHash, subscriberId, upToSequence)`.
- **On disconnect:** Remove from `_liveSubscribersByTopic` and `_liveDurableConnections`. Durable queue and `_durableSubscribersByTopic` entries persist.

### Client-Side API

`BoltClient` adds:

```csharp
// Transient subscribe - returns IAsyncEnumerable<T> backed by a Channel
public IAsyncEnumerable<T> SubscribeAsync<T>(string topic, CancellationToken ct = default);

// Durable subscribe - replay on reconnect, requires manual ack
public IAsyncEnumerable<DurableMessage<T>> SubscribeDurableAsync<T>(
    string topic,
    string subscriberId,
    CancellationToken ct = default);

// Publish - fire-and-forget. durable=true queues for durable subscribers; false fan-out only.
public ValueTask PublishAsync<T>(string topic, T payload, bool durable = false, CancellationToken ct = default);

// Explicit unsubscribe (cancelling the IAsyncEnumerable's CancellationToken also unsubscribes)
public ValueTask UnsubscribeAsync(string topic);

// Ack a durable message (or batch - DurableMessage carries an Ack() helper that calls this)
public ValueTask AckAsync(string topic, string subscriberId, long upToSequence, CancellationToken ct = default);
```

`DurableMessage<T>` wraps the payload with metadata:

```csharp
public sealed record DurableMessage<T>(T Payload, long Sequence, bool IsReplay)
{
    public ValueTask AckAsync(CancellationToken ct = default);  // Acks this message via the originating BoltClient
}
```

Consumers typically iterate and ack:

```csharp
await foreach (var msg in client.SubscribeDurableAsync<ChatMessage>("chat.room.42", "user-uuid-here", ct))
{
    await ProcessMessage(msg.Payload);
    await msg.AckAsync(ct);
}
```

For higher throughput, ack in batches (every N messages or every M seconds) by calling `client.AckAsync(topic, subscriberId, lastSeq)` directly instead of per-message acks.

`BoltDriver` translates these to the existing `IMessageBusWrapper` API:

```csharp
public Task SubscribeAsync<TResponse>(string topic, Action<TResponse> handler, CancellationToken ct)
{
    _ = Task.Run(async () =>
    {
        await foreach (var item in _client.SubscribeAsync<TResponse>(topic, ct))
            handler(item);
    }, ct);
    return Task.CompletedTask;
}

public Task SubscribeDurableAsync<TResponse>(
    string topic,
    string subscriberId,
    Func<TResponse, Task> handler,
    CancellationToken ct)
{
    _ = Task.Run(async () =>
    {
        await foreach (var msg in _client.SubscribeDurableAsync<TResponse>(topic, subscriberId, ct))
        {
            await handler(msg.Payload);
            await msg.AckAsync(ct);  // auto-ack after successful handler
        }
    }, ct);
    return Task.CompletedTask;
}

public Task PublishAsync<TRequest>(string topic, TRequest payload, bool durable = false)
    => _client.PublishAsync(topic, payload, durable).AsTask();
```

The new `SubscribeDurableAsync` is added to `IMessageBusWrapper` as well - it's the only interface change in this migration. All existing methods stay backward-compatible.

## BoltDriver Implementation

`BoltDriverSignalR` becomes `BoltDriver`. The `IMessageBusWrapper` interface stays identical.

```csharp
public sealed class BoltDriver : IMessageBusWrapper
{
    private readonly BoltClient _client;
    private readonly BoltConfiguration _config;
    private readonly ILogger<BoltDriver> _logger;

    public BoltDriver(BoltClient client, IOptions<BoltConfiguration> config, ILogger<BoltDriver> logger)
    {
        _client = client;
        _config = config.Value;
        _logger = logger;
    }

    public bool IsConnected => _client.IsConnected;
    // ... event handlers wired to BoltClient.OnConnected/OnDisconnected/OnReconnected
}
```

### Method Translation

| Current (SignalR) | New (Bolt) |
|---|---|
| `Connection.InvokeAsync<TResponse>("Invoke", BoltMessage)` | `_client.SendAsync<TRequest, TResponse>(recipient, request)` |
| `Connection.SendAsync("Push", BoltMessage)` | `_client.PushAsync<TRequest>(recipient, request)` |
| `Connection.On<T>("topic", handler)` | `_client.SubscribeAsync<T>(topic)` |
| `Connection.SendAsync("Subscribe", group)` | `_client.SubscribeAsync<T>(topic)` (returns IAsyncEnumerable) |

### Recipient Resolution

`BoltConfiguration.Targets` (the GUID-keyed map of service names -> service IDs) stays. `BoltDriver` looks up the target service ID from `Targets` and passes it to `BoltClient.SendAsync`. `BoltClient` FNV1a-hashes the recipient ID into the frame header for Hub routing.

### DI Registration

A single extension method in `XFramework.Integration` replaces both `SignalRService` and `BoltDriverSignalR` registration:

```csharp
public static IServiceCollection AddXFrameworkBoltClient(this IServiceCollection services, IConfiguration config)
{
    services.Configure<BoltConfiguration>(config.GetSection("BoltConfiguration"));
    services.AddBoltClient(opts =>
    {
        var bolt = config.GetSection("BoltConfiguration").Get<BoltConfiguration>();
        opts.ServerUri = new Uri(bolt.ServerUrls[0]);
        opts.ClientId = bolt.ClientGuid.ToString();
        opts.ClientName = bolt.ClientName;
    });
    services.AddSingleton<IMessageBusWrapper, BoltDriver>();
    return services;
}
```

Each service's `Program.cs` calls `services.AddXFrameworkBoltClient(builder.Configuration)` instead of the current SignalR registration.

## File Changes

### Deleted Files (`XFramework.Integration`)

```
Services/SignalRService.cs
Services/ConnectionPool.cs                    - subsumed by BoltClient's pool
Services/PooledRpcCall.cs                     - subsumed by BoltClient's tracking
Drivers/BaseSignalRHandler.cs
Drivers/BoltDriverSignalR.cs                  - replaced by BoltDriver
Abstractions/ISignalRService.cs
Abstractions/ISignalREventHandler.cs
```

### New Files (`XFramework.Integration`)

```
Drivers/BoltDriver.cs                          - IMessageBusWrapper over BoltClient
```

### Modified Files (`XFramework.Integration`)

```
Extensions/ServiceCollectionExtensions.cs      - AddXFrameworkBoltClient extension
XFramework.Integration.csproj                  - remove SignalR.Client, add Bolt.Net.Client reference
Services/Helpers/BoltHelper.cs                 - keep, still useful for MemoryPack helpers
```

### Deleted Files (`Bolt.Hub`)

```
Hubs/MessageQueueHub.cs                        - entire SignalR hub
```

### New Files (`Bolt.Hub`)

```
Durable/IDurableQueueStore.cs                  - abstraction for durable queue backend
Durable/RedisDurableQueueStore.cs              - Redis Streams implementation (preferred)
Durable/InMemoryDurableQueueStore.cs           - in-process fallback (logs warning)
Durable/DurableQueueOptions.cs                 - TTL, MaxQueueSize, MaxReplayBatchSize config
```

### Modified Files (`Bolt.Hub`)

```
Installers/BoltInstaller.cs                    - remove AddSignalR + MessagePack protocol; register IDurableQueueStore (Redis or in-memory based on config)
Extensions/ApplicationBuilderExtension.cs      - remove MapHub, keep MapBolt
Bolt.Hub.csproj                                - remove SignalR + MessagePack packages; add StackExchange.Redis (optional)
ThinProtocol/BoltServer.cs                     - add Subscribe/Unsubscribe/Publish/Ack frame handlers (transient + durable) + temp ExecuteQuery/ExecuteChanges handlers
Services/QueryExecutionService.cs              - keep as-is, called from new BoltServer frame handlers
appsettings.json                               - add Durable section (Redis connection string optional)
```

### Modified Files (`Bolt.Protocol`)

```
Protocol/FrameType.cs                          - add Subscribe (0x10), Unsubscribe (0x11), Publish (0x12), Event (0x13), ExecuteQuery (0x14), ExecuteChanges (0x15)
Protocol/BoltCodec.cs                          - encode/decode for new frame types
```

### Modified Files (`Bolt.Client`)

```
BoltClient.cs                                  - SubscribeAsync<T>, SubscribeDurableAsync<T>, PublishAsync<T>, UnsubscribeAsync, AckAsync; reconnect re-sends Subscribe frames
DurableMessage.cs                              - record wrapping payload + sequence + IsReplay + AckAsync helper
```

### Modified Files (Service Configs)

All 8 service `appsettings.json` + `appsettings.Development.json` + `appsettings.Docker.json` files change `ServerUrls`:

```
Before: "http://localhost:7000/stream-flow/queue"
After:  "ws://localhost:7000/bolt/ws"

Docker before: "http://bolt-hub:8080/stream-flow/queue"
Docker after:  "ws://bolt-hub:8080/bolt/ws"
```

Affected services: IdentityServer, Wallets, Communications, Community, SmsGateway, Inventario, Coins, Gateway, XFramework.Portal.

## Testing

### Integration Tests

Existing fixtures (`IdentityServer.IntegrationTests`, `Wallets.IntegrationTests`) need updates:
- Test fixture switches `MapHub<MessageQueueHub>` -> `MapBolt`
- Service URLs change from `http://localhost:.../stream-flow/queue` -> `ws://localhost:.../bolt/ws`
- The `IMessageBusWrapper`-based test code works unchanged
- Verify all existing tests pass after the swap

### New Tests

**Transient pub/sub:**

| Test | Verifies |
|---|---|
| `BoltDriverRpcTests` | `IMessageBusWrapper.SendAsync` round-trip via BoltClient |
| `BoltDriverPushTests` | Fire-and-forget `PushAsync` |
| `BoltPubSubTests_BasicFlow` | Subscribe -> Publish -> Subscriber receives Event |
| `BoltPubSubTests_PublisherDoesNotReceiveOwnMessages` | Publisher does not receive its own published messages |
| `BoltPubSubTests_MultipleSubscribers` | All subscribers to a topic receive the published message |
| `BoltPubSubTests_UnsubscribeStopsDelivery` | Unsubscribed client stops receiving |
| `BoltPubSubTests_ReconnectResubscribes` | After client reconnect, transient subscriptions are re-sent and continue working |
| `BoltPubSubTests_DisconnectCleansUp` | Hub removes all transient subscriptions when a client disconnects |
| `BoltServerSubscribeFrameTests` | Server-side Subscribe/Unsubscribe/Publish frame parsing and routing |

**Durable pub/sub (in-memory store - covered by integration tests; Redis store covered by separate fixture if Redis is available):**

| Test | Verifies |
|---|---|
| `BoltDurablePubSubTests_BasicFlow` | Durable subscribe -> publish (durable=true) -> receive with sequence number -> ack |
| `BoltDurablePubSubTests_OfflineMessagesQueued` | Subscribe durable, disconnect, publish 5 messages, reconnect, receive all 5 with replay flag in order |
| `BoltDurablePubSubTests_AckTrimsQueue` | Acked messages are removed; unacked are re-delivered on reconnect |
| `BoltDurablePubSubTests_ReplayThenLive` | After reconnect, replayed messages arrive before live messages even if live messages are published mid-replay |
| `BoltDurablePubSubTests_NonDurablePublishNotQueued` | Publish with `durable=false` is not queued for durable subscribers (live fan-out only) |
| `BoltDurablePubSubTests_TransientSubscriberIgnoresQueue` | Transient subscribers do not receive replayed messages on reconnect |
| `BoltDurablePubSubTests_MaxQueueSizeDropsOldest` | When queue exceeds `MaxQueueSize`, oldest messages are dropped |
| `BoltDurablePubSubTests_MultipleDurableSubscribersIndependentQueues` | Each `(topic, subscriberId)` pair has its own queue and independent ack state |
| `BoltDurableQueueStoreTests_InMemory` | In-memory store contract tests (append, read, ack, trim) |
| `BoltDurableQueueStoreTests_Redis` | Redis store contract tests (skipped if Redis not available) |

### Benchmarks

`IdentityServer.Benchmarks` already compares Bolt thin protocol vs HTTP. After this migration there's only one Bolt path - the SignalR comparison goes away. Benchmark suite updates:
- Remove SignalR variants from `TransportBenchmarks`
- Add a `Bolt_PubSub_Throughput` benchmark measuring publish throughput with N subscribers

## Migration Order

The migration must happen in a specific order to avoid breaking existing services:

1. **Add pub/sub frame types to `Bolt.Protocol`** - non-breaking, just new enum values (Subscribe, Unsubscribe, Publish, Event, Ack, ExecuteQuery, ExecuteChanges)
2. **Add `IDurableQueueStore` interface + `InMemoryDurableQueueStore` to `Bolt.Hub`** - non-breaking
3. **Add `RedisDurableQueueStore` to `Bolt.Hub`** - non-breaking, opt-in via config
4. **Add transient pub/sub support to `BoltClient`** - non-breaking, just new methods
5. **Add transient pub/sub frame handlers to `BoltServer`** - non-breaking, just new handlers
6. **Add durable pub/sub support to `BoltClient` (`SubscribeDurableAsync`, `AckAsync`, `DurableMessage<T>`)** - non-breaking
7. **Add durable pub/sub frame handlers to `BoltServer` (replay-on-reconnect, ack handling)** - non-breaking
8. **Add `ExecuteQuery`/`ExecuteChanges` shim handlers to `BoltServer`** - non-breaking
9. **Create new `BoltDriver` (alongside `BoltDriverSignalR`)** - non-breaking, doesn't replace yet
10. **Switch DI registration in one service** (IdentityServer) - verify it works end-to-end
11. **Switch remaining services one by one** - Wallets, Communications, Community, SmsGateway, Inventario, XFramework.Portal, Gateway, Coins
12. **Delete `BoltDriverSignalR`, `SignalRService`, `ConnectionPool`, `PooledRpcCall`, `BaseSignalRHandler`, `ISignalRService`, `ISignalREventHandler`**
13. **Delete `MessageQueueHub`** and remove SignalR registration from `Bolt.Hub`
14. **Remove SignalR package references** from `Bolt.Hub.csproj` and `XFramework.Integration.csproj`
15. **Update integration tests** to use `MapBolt` and verify all pass

After step 15, no SignalR code or dependencies remain in the codebase.

## Verification

- All existing integration tests pass after the migration
- New transient pub/sub tests pass (9 tests)
- New durable pub/sub tests pass (10 tests)
- `dotnet build` solution-wide: 0 errors
- No references to `Microsoft.AspNetCore.SignalR.*` in any csproj
- No references to `SignalRService`, `ISignalRService`, `MessageQueueHub`, `BoltDriverSignalR`, `BaseSignalRHandler`, `ISignalREventHandler` in any source file
- IdentityServer benchmarks still pass and show comparable or better throughput vs the SignalR baseline
- Durable subscription benchmark: measure throughput and replay latency for 10K queued messages on reconnect
