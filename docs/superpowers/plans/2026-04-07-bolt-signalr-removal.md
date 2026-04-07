# Bolt SignalR Removal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace SignalR with the existing Bolt thin protocol for service-to-service RPC, add native pub/sub support (transient + durable), and delete all SignalR code from client and Hub.

**Architecture:** All services already implement `IMessageBusWrapper` (the contract). The current implementation `BoltDriverSignalR` uses SignalR `HubConnection`. This plan replaces it with `BoltDriver` that wraps the existing `BoltClient` (Bolt.Client library, thin binary protocol over WebSocket). The Hub gets new pub/sub frame handlers (transient fan-out + durable queues with explicit ack and reconnect replay). Storage backend for durable queues is Redis (with in-memory fallback).

**Tech Stack:** .NET 10, C# 14, MemoryPack (payload serialization), `IBufferWriter<byte>` (zero-alloc encoding), `BoltCodec` (binary frame codec), `BoltClient` (existing thin protocol client), Redis Streams (`XADD`/`XREAD`/`XTRIM` for durable queues, optional via `StackExchange.Redis`).

---

## File Structure

### New files

```
src/Modules/XFramework.Bolt/Bolt.Hub/Durable/
├── IDurableQueueStore.cs              # Abstraction for durable queue backend
├── InMemoryDurableQueueStore.cs       # In-process fallback (warns at startup)
├── RedisDurableQueueStore.cs          # Redis Streams backend (preferred)
└── DurableQueueOptions.cs             # TTL, MaxQueueSize, MaxReplayBatchSize

src/Libraries/Bolt/Bolt.Client/
└── DurableMessage.cs                  # Wrapper: payload + sequence + IsReplay + AckAsync helper

src/Infrastructure/XFramework.Integration/Drivers/
└── BoltDriver.cs                      # New IMessageBusWrapper impl over BoltClient
```

### Modified files

```
src/Libraries/Bolt/Bolt.Protocol/Protocol/FrameType.cs               # Add Subscribe, Unsubscribe, Publish, Event, Ack, ExecuteQuery, ExecuteChanges
src/Libraries/Bolt/Bolt.Protocol/Protocol/BoltCodec.cs               # Encode/decode for new frame types
src/Libraries/Bolt/Bolt.Client/BoltClient.cs                         # SubscribeAsync, SubscribeDurableAsync, PublishAsync, UnsubscribeAsync, AckAsync; reconnect re-sends Subscribe
src/Modules/XFramework.Bolt/Bolt.Hub/ThinProtocol/BoltServer.cs      # Subscribe/Unsubscribe/Publish/Ack frame handlers (transient + durable)
src/Modules/XFramework.Bolt/Bolt.Hub/Installers/BoltInstaller.cs     # Remove AddSignalR; register IDurableQueueStore
src/Modules/XFramework.Bolt/Bolt.Hub/Extensions/ApplicationBuilderExtension.cs   # Remove MapHub<MessageQueueHub>
src/Modules/XFramework.Bolt/Bolt.Hub/Bolt.Hub.csproj                 # Remove SignalR refs, add StackExchange.Redis
src/Modules/XFramework.Bolt/Bolt.Hub/appsettings.json                # Add Durable section
src/Infrastructure/XFramework.Integration/Extensions/ServiceCollectionExtensions.cs  # AddXFrameworkBoltClient extension
src/Infrastructure/XFramework.Integration/XFramework.Integration.csproj           # Remove SignalR.Client, add Bolt.Net.Client reference
src/Shared/XFramework.Domain.Shared/Configurations/BoltConfiguration.cs           # Add Durable subsection
src/Infrastructure/XFramework.Integration/Abstractions/Wrappers/IMessageBusWrapper.cs  # Add SubscribeDurableAsync method
Directory.Packages.props                                              # Add StackExchange.Redis
```

### Deleted files

```
src/Modules/XFramework.Bolt/Bolt.Hub/Hubs/MessageQueueHub.cs
src/Infrastructure/XFramework.Integration/Services/SignalRService.cs
src/Infrastructure/XFramework.Integration/Services/ConnectionPool.cs
src/Infrastructure/XFramework.Integration/Services/PooledRpcCall.cs
src/Infrastructure/XFramework.Integration/Drivers/BaseSignalRHandler.cs
src/Infrastructure/XFramework.Integration/Drivers/BoltDriverSignalR.cs
src/Infrastructure/XFramework.Integration/Abstractions/ISignalRService.cs
src/Infrastructure/XFramework.Integration/Abstractions/ISignalREventHandler.cs
```

### Modified service configs (per-service URL update)

Each service's `appsettings.json`, `appsettings.Development.json`, and `appsettings.Docker.json` (where present):

```
src/Modules/XFramework.IdentityServer/IdentityServer.Api/
src/Modules/XFramework.Wallets/Wallets.Api/
src/Modules/XFramework.Messaging/Messaging.Api/
src/Modules/XFramework.Community/Community.Api/
src/Modules/XFramework.SmsGateway/SmsGateway.Api/
src/Modules/XFramework.Inventario/Inventario.Api/
src/Modules/XFramework.Coins/Server/Coins.Api/
src/Presentation/Gateway/
src/Presentation/ControlPanel.Server/
```

---

## Frame Type Slot Allocation

The new frame types slot into the unused range between `Push (0x05)` and `StreamOpen (0x10)`:

| Frame | Hex | Direction |
|---|---|---|
| `Subscribe` | 0x06 | Client → Hub |
| `Unsubscribe` | 0x07 | Client → Hub |
| `Publish` | 0x08 | Client → Hub |
| `Event` | 0x09 | Hub → Client |
| `Ack` | 0x0A | Client → Hub |
| `ExecuteQuery` | 0x0B | Client → Hub (DB proxy shim) |
| `ExecuteChanges` | 0x0C | Client → Hub (DB proxy shim) |

This avoids conflict with media frames (0x20+) and stream frames (0x10+).

---

## Task 1: Add Pub/Sub Frame Types to Protocol

**Files:**
- Modify: `src/Libraries/Bolt/Bolt.Protocol/Protocol/FrameType.cs`

- [ ] **Step 1: Add new enum values**

In `src/Libraries/Bolt/Bolt.Protocol/Protocol/FrameType.cs`, add the following entries after `Push = 0x05` and before `StreamOpen = 0x10`:

```csharp
    /// <summary>Subscribe to a topic: [1:type] [4:topicHash] [1:flags] [4:subscriberIdLen] [subscriberId] [4:topicLen] [topic]</summary>
    Subscribe = 0x06,
    /// <summary>Unsubscribe from a topic: [1:type] [4:topicHash] [4:subscriberIdLen] [subscriberId]</summary>
    Unsubscribe = 0x07,
    /// <summary>Publish to a topic: [1:type] [4:topicHash] [1:flags] [4:payloadLen] [payload]</summary>
    Publish = 0x08,
    /// <summary>Hub-delivered event: [1:type] [4:topicHash] [8:sequenceNumber] [1:flags] [4:payloadLen] [payload]</summary>
    Event = 0x09,
    /// <summary>Acknowledge durable messages: [1:type] [4:topicHash] [4:subscriberIdLen] [subscriberId] [8:upToSequenceNumber]</summary>
    Ack = 0x0A,
    /// <summary>Hub-side query execution (transitional shim): [1:type] [16:requestId] [4:payloadLen] [payload]</summary>
    ExecuteQuery = 0x0B,
    /// <summary>Hub-side change execution (transitional shim): [1:type] [16:requestId] [4:payloadLen] [payload]</summary>
    ExecuteChanges = 0x0C,
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Libraries/Bolt/Bolt.Protocol/Bolt.Protocol.csproj`
Expected: Build succeeds with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/Libraries/Bolt/Bolt.Protocol/Protocol/FrameType.cs
git commit -m "feat(bolt-protocol): add Subscribe/Unsubscribe/Publish/Event/Ack/ExecuteQuery/ExecuteChanges frame types"
```

---

## Task 2: Add Pub/Sub Codec Methods

**Files:**
- Modify: `src/Libraries/Bolt/Bolt.Protocol/Protocol/BoltCodec.cs`

### Context for implementer

`BoltCodec` is a static class with `Write*` (encode to `IBufferWriter<byte>`) and `TryRead*` (decode from `ReadOnlySpan<byte>`) methods. Use the existing patterns. All multi-byte values are little-endian (`BinaryPrimitives.WriteInt32LittleEndian`, `BinaryPrimitives.WriteInt64LittleEndian`). Topic hash is `Fnv1aHash(topic)` — same FNV-1a 32-bit hash already used for service routing.

The flag byte:
- Subscribe: bit 0 = durable
- Publish: bit 0 = durable-eligible
- Event: bit 0 = replay

`subscriberId` and `topic` are UTF-8 strings.

- [ ] **Step 1: Add header size constants**

In `src/Libraries/Bolt/Bolt.Protocol/Protocol/BoltCodec.cs`, find the existing constants section (near the top, after `RequestHeaderSize` and `ResponseHeaderSize`). Add:

```csharp
    // Pub/sub header sizes (variable for Subscribe/Unsubscribe/Ack due to subscriberId)
    public const int PublishHeaderSize = 1 + 4 + 1 + 4;            // 10 bytes + payload
    public const int EventHeaderSize = 1 + 4 + 8 + 1 + 4;          // 18 bytes + payload
    // Subscribe header is variable: 1 + 4 + 1 + 4 + N + 4 + M (10 + subscriberId + topic)
    // Unsubscribe header is variable: 1 + 4 + 4 + N (9 + subscriberId)
    // Ack header is variable: 1 + 4 + 4 + N + 8 (17 + subscriberId)

    // ExecuteQuery/ExecuteChanges shim (DB proxy transitional)
    public const int ExecuteQueryHeaderSize = 1 + 16 + 4;          // 21 bytes + payload
    public const int ExecuteChangesHeaderSize = 1 + 16 + 4;        // 21 bytes + payload
```

- [ ] **Step 2: Add WriteSubscribe encoder**

In the encoding region of `BoltCodec.cs`, after `WritePush`, add:

```csharp
    /// <summary>
    /// Encode a Subscribe frame: [1:type=0x06] [4:topicHash] [1:flags] [4:subscriberIdLen] [subscriberId UTF-8] [4:topicLen] [topic UTF-8]
    /// </summary>
    public static int WriteSubscribe(IBufferWriter<byte> writer, string topic, string subscriberId, bool durable)
    {
        var topicBytes = Encoding.UTF8.GetByteCount(topic);
        var idBytes = Encoding.UTF8.GetByteCount(subscriberId);
        var totalSize = 1 + 4 + 1 + 4 + idBytes + 4 + topicBytes;
        var span = writer.GetSpan(totalSize);

        span[0] = (byte)FrameType.Subscribe;
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(1), Fnv1aHash(topic));
        span[5] = (byte)(durable ? 0x01 : 0x00);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(6), idBytes);
        Encoding.UTF8.GetBytes(subscriberId, span.Slice(10));
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(10 + idBytes), topicBytes);
        Encoding.UTF8.GetBytes(topic, span.Slice(14 + idBytes));

        writer.Advance(totalSize);
        return totalSize;
    }
```

- [ ] **Step 3: Add WriteUnsubscribe encoder**

Add immediately after `WriteSubscribe`:

```csharp
    /// <summary>
    /// Encode an Unsubscribe frame: [1:type=0x07] [4:topicHash] [4:subscriberIdLen] [subscriberId UTF-8]
    /// </summary>
    public static int WriteUnsubscribe(IBufferWriter<byte> writer, string topic, string subscriberId)
    {
        var idBytes = Encoding.UTF8.GetByteCount(subscriberId);
        var totalSize = 1 + 4 + 4 + idBytes;
        var span = writer.GetSpan(totalSize);

        span[0] = (byte)FrameType.Unsubscribe;
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(1), Fnv1aHash(topic));
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(5), idBytes);
        Encoding.UTF8.GetBytes(subscriberId, span.Slice(9));

        writer.Advance(totalSize);
        return totalSize;
    }
```

- [ ] **Step 4: Add WritePublish encoder**

Add immediately after `WriteUnsubscribe`:

```csharp
    /// <summary>
    /// Encode a Publish frame: [1:type=0x08] [4:topicHash] [1:flags] [4:payloadLen] [payload]
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WritePublish(IBufferWriter<byte> writer, string topic, bool durableEligible, ReadOnlySpan<byte> payload)
    {
        var totalSize = PublishHeaderSize + payload.Length;
        var span = writer.GetSpan(totalSize);

        span[0] = (byte)FrameType.Publish;
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(1), Fnv1aHash(topic));
        span[5] = (byte)(durableEligible ? 0x01 : 0x00);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(6), payload.Length);
        payload.CopyTo(span.Slice(10));

        writer.Advance(totalSize);
        return totalSize;
    }
```

- [ ] **Step 5: Add WriteEvent encoder**

Add immediately after `WritePublish`:

```csharp
    /// <summary>
    /// Encode an Event frame: [1:type=0x09] [4:topicHash] [8:sequenceNumber] [1:flags] [4:payloadLen] [payload]
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteEvent(IBufferWriter<byte> writer, int topicHash, long sequenceNumber, bool isReplay, ReadOnlySpan<byte> payload)
    {
        var totalSize = EventHeaderSize + payload.Length;
        var span = writer.GetSpan(totalSize);

        span[0] = (byte)FrameType.Event;
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(1), topicHash);
        BinaryPrimitives.WriteInt64LittleEndian(span.Slice(5), sequenceNumber);
        span[13] = (byte)(isReplay ? 0x01 : 0x00);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(14), payload.Length);
        payload.CopyTo(span.Slice(18));

        writer.Advance(totalSize);
        return totalSize;
    }
```

- [ ] **Step 6: Add WriteAck encoder**

Add immediately after `WriteEvent`:

```csharp
    /// <summary>
    /// Encode an Ack frame: [1:type=0x0A] [4:topicHash] [4:subscriberIdLen] [subscriberId UTF-8] [8:upToSequenceNumber]
    /// </summary>
    public static int WriteAck(IBufferWriter<byte> writer, int topicHash, string subscriberId, long upToSequenceNumber)
    {
        var idBytes = Encoding.UTF8.GetByteCount(subscriberId);
        var totalSize = 1 + 4 + 4 + idBytes + 8;
        var span = writer.GetSpan(totalSize);

        span[0] = (byte)FrameType.Ack;
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(1), topicHash);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(5), idBytes);
        Encoding.UTF8.GetBytes(subscriberId, span.Slice(9));
        BinaryPrimitives.WriteInt64LittleEndian(span.Slice(9 + idBytes), upToSequenceNumber);

        writer.Advance(totalSize);
        return totalSize;
    }
```

- [ ] **Step 7: Add TryReadSubscribe decoder**

In the decoding region of `BoltCodec.cs` (after the existing `TryRead*` methods), add:

```csharp
    /// <summary>
    /// Decode a Subscribe frame.
    /// </summary>
    public static bool TryReadSubscribe(ReadOnlySpan<byte> buffer, out int topicHash, out bool durable, out string subscriberId, out string topic, out int bytesConsumed)
    {
        topicHash = 0;
        durable = false;
        subscriberId = string.Empty;
        topic = string.Empty;
        bytesConsumed = 0;

        if (buffer.Length < 14) return false;
        if (buffer[0] != (byte)FrameType.Subscribe) return false;

        topicHash = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(1));
        durable = (buffer[5] & 0x01) != 0;
        var idLen = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(6));
        if (idLen < 0 || idLen > 4096) return false;
        if (buffer.Length < 10 + idLen + 4) return false;

        subscriberId = Encoding.UTF8.GetString(buffer.Slice(10, idLen));
        var topicLen = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(10 + idLen));
        if (topicLen < 0 || topicLen > 4096) return false;
        if (buffer.Length < 14 + idLen + topicLen) return false;

        topic = Encoding.UTF8.GetString(buffer.Slice(14 + idLen, topicLen));
        bytesConsumed = 14 + idLen + topicLen;
        return true;
    }
```

- [ ] **Step 8: Add TryReadUnsubscribe decoder**

```csharp
    /// <summary>
    /// Decode an Unsubscribe frame.
    /// </summary>
    public static bool TryReadUnsubscribe(ReadOnlySpan<byte> buffer, out int topicHash, out string subscriberId, out int bytesConsumed)
    {
        topicHash = 0;
        subscriberId = string.Empty;
        bytesConsumed = 0;

        if (buffer.Length < 9) return false;
        if (buffer[0] != (byte)FrameType.Unsubscribe) return false;

        topicHash = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(1));
        var idLen = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(5));
        if (idLen < 0 || idLen > 4096) return false;
        if (buffer.Length < 9 + idLen) return false;

        subscriberId = Encoding.UTF8.GetString(buffer.Slice(9, idLen));
        bytesConsumed = 9 + idLen;
        return true;
    }
```

- [ ] **Step 9: Add TryReadPublish decoder**

```csharp
    /// <summary>
    /// Decode a Publish frame. Returns offset/length into the source buffer (zero-copy).
    /// </summary>
    public static bool TryReadPublish(ReadOnlySpan<byte> buffer, out int topicHash, out bool durableEligible, out int payloadOffset, out int payloadLength, out int totalSize)
    {
        topicHash = 0;
        durableEligible = false;
        payloadOffset = 0;
        payloadLength = 0;
        totalSize = 0;

        if (buffer.Length < PublishHeaderSize) return false;
        if (buffer[0] != (byte)FrameType.Publish) return false;

        topicHash = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(1));
        durableEligible = (buffer[5] & 0x01) != 0;
        payloadLength = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(6));
        if (payloadLength < 0 || payloadLength > 100 * 1024 * 1024) return false;

        payloadOffset = PublishHeaderSize;
        totalSize = PublishHeaderSize + payloadLength;
        return buffer.Length >= totalSize;
    }
```

- [ ] **Step 10: Add TryReadEvent decoder**

```csharp
    /// <summary>
    /// Decode an Event frame. Returns offset/length into the source buffer (zero-copy).
    /// </summary>
    public static bool TryReadEvent(ReadOnlySpan<byte> buffer, out int topicHash, out long sequenceNumber, out bool isReplay, out int payloadOffset, out int payloadLength, out int totalSize)
    {
        topicHash = 0;
        sequenceNumber = 0;
        isReplay = false;
        payloadOffset = 0;
        payloadLength = 0;
        totalSize = 0;

        if (buffer.Length < EventHeaderSize) return false;
        if (buffer[0] != (byte)FrameType.Event) return false;

        topicHash = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(1));
        sequenceNumber = BinaryPrimitives.ReadInt64LittleEndian(buffer.Slice(5));
        isReplay = (buffer[13] & 0x01) != 0;
        payloadLength = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(14));
        if (payloadLength < 0 || payloadLength > 100 * 1024 * 1024) return false;

        payloadOffset = EventHeaderSize;
        totalSize = EventHeaderSize + payloadLength;
        return buffer.Length >= totalSize;
    }
```

- [ ] **Step 11: Add TryReadAck decoder**

```csharp
    /// <summary>
    /// Decode an Ack frame.
    /// </summary>
    public static bool TryReadAck(ReadOnlySpan<byte> buffer, out int topicHash, out string subscriberId, out long upToSequenceNumber, out int bytesConsumed)
    {
        topicHash = 0;
        subscriberId = string.Empty;
        upToSequenceNumber = 0;
        bytesConsumed = 0;

        if (buffer.Length < 9) return false;
        if (buffer[0] != (byte)FrameType.Ack) return false;

        topicHash = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(1));
        var idLen = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(5));
        if (idLen < 0 || idLen > 4096) return false;
        if (buffer.Length < 9 + idLen + 8) return false;

        subscriberId = Encoding.UTF8.GetString(buffer.Slice(9, idLen));
        upToSequenceNumber = BinaryPrimitives.ReadInt64LittleEndian(buffer.Slice(9 + idLen));
        bytesConsumed = 9 + idLen + 8;
        return true;
    }
```

- [ ] **Step 12: Verify build**

Run: `dotnet build src/Libraries/Bolt/Bolt.Protocol/Bolt.Protocol.csproj`
Expected: Build succeeds with 0 errors.

- [ ] **Step 13: Write codec round-trip tests**

Create or open `src/Tests/Bolt.Tests/BoltCodecPubSubTests.cs`:

```csharp
using System.Buffers;
using Bolt.Protocol;
using Bolt.Protocol.Buffers;
using FluentAssertions;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public class BoltCodecPubSubTests
{
    [Test]
    public void Subscribe_RoundTrip_DurableTrue()
    {
        var writer = new RentedBufferWriter();
        BoltCodec.WriteSubscribe(writer, "chat.room.42", "user-abc", durable: true);

        var ok = BoltCodec.TryReadSubscribe(writer.WrittenSpan, out var topicHash, out var durable, out var subscriberId, out var topic, out var consumed);

        ok.Should().BeTrue();
        topicHash.Should().Be(BoltCodec.Fnv1aHash("chat.room.42"));
        durable.Should().BeTrue();
        subscriberId.Should().Be("user-abc");
        topic.Should().Be("chat.room.42");
        consumed.Should().Be(writer.WrittenCount);
    }

    [Test]
    public void Subscribe_RoundTrip_DurableFalse()
    {
        var writer = new RentedBufferWriter();
        BoltCodec.WriteSubscribe(writer, "presence", "client-1", durable: false);

        BoltCodec.TryReadSubscribe(writer.WrittenSpan, out _, out var durable, out _, out _, out _).Should().BeTrue();
        durable.Should().BeFalse();
    }

    [Test]
    public void Unsubscribe_RoundTrip()
    {
        var writer = new RentedBufferWriter();
        BoltCodec.WriteUnsubscribe(writer, "chat.room.42", "user-abc");

        var ok = BoltCodec.TryReadUnsubscribe(writer.WrittenSpan, out var topicHash, out var subscriberId, out var consumed);

        ok.Should().BeTrue();
        topicHash.Should().Be(BoltCodec.Fnv1aHash("chat.room.42"));
        subscriberId.Should().Be("user-abc");
        consumed.Should().Be(writer.WrittenCount);
    }

    [Test]
    public void Publish_RoundTrip_DurableEligible()
    {
        var payload = new byte[] { 1, 2, 3, 4, 5 };
        var writer = new RentedBufferWriter();
        BoltCodec.WritePublish(writer, "chat.room.42", durableEligible: true, payload);

        var ok = BoltCodec.TryReadPublish(writer.WrittenSpan, out var topicHash, out var durableEligible, out var payloadOffset, out var payloadLength, out var totalSize);

        ok.Should().BeTrue();
        topicHash.Should().Be(BoltCodec.Fnv1aHash("chat.room.42"));
        durableEligible.Should().BeTrue();
        payloadLength.Should().Be(payload.Length);
        totalSize.Should().Be(writer.WrittenCount);
        writer.WrittenSpan.Slice(payloadOffset, payloadLength).ToArray().Should().Equal(payload);
    }

    [Test]
    public void Event_RoundTrip_WithSequenceAndReplay()
    {
        var payload = new byte[] { 9, 8, 7 };
        var writer = new RentedBufferWriter();
        BoltCodec.WriteEvent(writer, BoltCodec.Fnv1aHash("topic-x"), sequenceNumber: 42, isReplay: true, payload);

        var ok = BoltCodec.TryReadEvent(writer.WrittenSpan, out var topicHash, out var seq, out var isReplay, out var off, out var len, out var total);

        ok.Should().BeTrue();
        topicHash.Should().Be(BoltCodec.Fnv1aHash("topic-x"));
        seq.Should().Be(42);
        isReplay.Should().BeTrue();
        len.Should().Be(payload.Length);
        writer.WrittenSpan.Slice(off, len).ToArray().Should().Equal(payload);
    }

    [Test]
    public void Ack_RoundTrip()
    {
        var writer = new RentedBufferWriter();
        BoltCodec.WriteAck(writer, BoltCodec.Fnv1aHash("topic-x"), "subscriber-7", upToSequenceNumber: 100);

        var ok = BoltCodec.TryReadAck(writer.WrittenSpan, out var topicHash, out var sid, out var upTo, out var consumed);

        ok.Should().BeTrue();
        topicHash.Should().Be(BoltCodec.Fnv1aHash("topic-x"));
        sid.Should().Be("subscriber-7");
        upTo.Should().Be(100);
        consumed.Should().Be(writer.WrittenCount);
    }
}
```

- [ ] **Step 14: Run codec tests**

Run: `dotnet test src/Tests/Bolt.Tests/Bolt.Tests.csproj --filter "FullyQualifiedName~BoltCodecPubSubTests"`
Expected: 6 tests pass.

- [ ] **Step 15: Commit**

```bash
git add src/Libraries/Bolt/Bolt.Protocol/Protocol/BoltCodec.cs src/Tests/Bolt.Tests/BoltCodecPubSubTests.cs
git commit -m "feat(bolt-protocol): add pub/sub codec methods + round-trip tests"
```

---

## Task 3: Durable Queue Store Abstraction + In-Memory Implementation

**Files:**
- Create: `src/Modules/XFramework.Bolt/Bolt.Hub/Durable/IDurableQueueStore.cs`
- Create: `src/Modules/XFramework.Bolt/Bolt.Hub/Durable/DurableQueueOptions.cs`
- Create: `src/Modules/XFramework.Bolt/Bolt.Hub/Durable/InMemoryDurableQueueStore.cs`
- Create: `src/Tests/Bolt.Tests/InMemoryDurableQueueStoreTests.cs`

### Context for implementer

The store is keyed by `(topicHash, subscriberId)`. Each key has its own monotonically-increasing sequence number. `AppendAsync` returns the new sequence number. `ReadFromAsync` returns unacked messages from `fromSequence + 1`. `AckAsync` removes messages up to and including `upToSequence`. `RegisterDurableSubscriberAsync` is idempotent — it ensures the (topic, subscriber) pair is known so future publishes know to enqueue for it.

- [ ] **Step 1: Create DurableQueueOptions**

Create `src/Modules/XFramework.Bolt/Bolt.Hub/Durable/DurableQueueOptions.cs`:

```csharp
namespace Bolt.Hub.Durable;

/// <summary>
/// Configuration for durable subscription queues.
/// </summary>
public sealed class DurableQueueOptions
{
    /// <summary>Optional Redis connection string. If null, in-memory store is used.</summary>
    public string? RedisConnectionString { get; set; }

    /// <summary>Time-to-live for queued messages in seconds. Default 7 days.</summary>
    public int MessageTtlSeconds { get; set; } = 604_800;

    /// <summary>Maximum messages per (topic, subscriber) queue. Oldest are dropped when exceeded.</summary>
    public int MaxQueueSize { get; set; } = 10_000;

    /// <summary>Maximum messages replayed in a single batch on reconnect.</summary>
    public int MaxReplayBatchSize { get; set; } = 1_000;
}
```

- [ ] **Step 2: Create IDurableQueueStore interface**

Create `src/Modules/XFramework.Bolt/Bolt.Hub/Durable/IDurableQueueStore.cs`:

```csharp
namespace Bolt.Hub.Durable;

/// <summary>
/// Backend for durable subscription queues. Each (topicHash, subscriberId) has its own
/// monotonically-increasing sequence-numbered queue.
/// </summary>
public interface IDurableQueueStore
{
    /// <summary>
    /// Append a message to the queue for (topicHash, subscriberId). Returns the assigned sequence number.
    /// Trims oldest messages when queue exceeds MaxQueueSize.
    /// </summary>
    Task<long> AppendAsync(int topicHash, string subscriberId, ReadOnlyMemory<byte> payload, CancellationToken ct = default);

    /// <summary>
    /// Read up to maxCount unacked messages starting from (fromSequence + 1).
    /// Returns (sequence, payload) pairs in sequence order.
    /// </summary>
    IAsyncEnumerable<(long Sequence, byte[] Payload)> ReadFromAsync(int topicHash, string subscriberId, long fromSequence, int maxCount, CancellationToken ct = default);

    /// <summary>
    /// Mark all messages up to and including upToSequence as acked. They are removed from the queue.
    /// </summary>
    Task AckAsync(int topicHash, string subscriberId, long upToSequence, CancellationToken ct = default);

    /// <summary>
    /// Idempotently register that (topicHash, subscriberId) is a durable subscriber for this topic.
    /// Future publishes to this topic will enqueue for this subscriber.
    /// </summary>
    Task RegisterDurableSubscriberAsync(int topicHash, string subscriberId, CancellationToken ct = default);

    /// <summary>
    /// Get all subscriberIds currently registered as durable for the given topic.
    /// Used by publish to know which queues to enqueue into.
    /// </summary>
    Task<IReadOnlyList<string>> GetDurableSubscribersAsync(int topicHash, CancellationToken ct = default);

    /// <summary>
    /// Get the last sequence number this subscriber acked. Returns 0 if no ack yet.
    /// Used on reconnect to find the starting point for replay.
    /// </summary>
    Task<long> GetLastAckedSequenceAsync(int topicHash, string subscriberId, CancellationToken ct = default);
}
```

- [ ] **Step 3: Create InMemoryDurableQueueStore**

Create `src/Modules/XFramework.Bolt/Bolt.Hub/Durable/InMemoryDurableQueueStore.cs`:

```csharp
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bolt.Hub.Durable;

/// <summary>
/// In-process durable queue store. Messages do not survive Hub restarts.
/// Used as a fallback when Redis is not configured.
/// </summary>
public sealed class InMemoryDurableQueueStore : IDurableQueueStore
{
    private readonly DurableQueueOptions _options;
    private readonly ILogger<InMemoryDurableQueueStore> _logger;

    // Per-(topicHash, subscriberId) queue with its own lock and sequence counter
    private readonly ConcurrentDictionary<(int TopicHash, string SubscriberId), QueueState> _queues = new();

    // Per-topic set of registered durable subscriberIds
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<string, byte>> _subscribers = new();

    public InMemoryDurableQueueStore(IOptions<DurableQueueOptions> options, ILogger<InMemoryDurableQueueStore> logger)
    {
        _options = options.Value;
        _logger = logger;
        _logger.LogWarning("Using in-memory durable queue store. Messages will be lost on Hub restart. Configure Redis for production.");
    }

    public Task<long> AppendAsync(int topicHash, string subscriberId, ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        var state = _queues.GetOrAdd((topicHash, subscriberId), _ => new QueueState());
        long seq;
        lock (state.Lock)
        {
            seq = ++state.NextSequence;
            state.Messages.Add((seq, payload.ToArray()));
            // Trim to MaxQueueSize
            while (state.Messages.Count > _options.MaxQueueSize)
                state.Messages.RemoveAt(0);
        }
        return Task.FromResult(seq);
    }

    public async IAsyncEnumerable<(long Sequence, byte[] Payload)> ReadFromAsync(int topicHash, string subscriberId, long fromSequence, int maxCount, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!_queues.TryGetValue((topicHash, subscriberId), out var state))
            yield break;

        List<(long, byte[])> snapshot;
        lock (state.Lock)
        {
            snapshot = state.Messages
                .Where(m => m.Sequence > fromSequence)
                .Take(maxCount)
                .ToList();
        }

        foreach (var msg in snapshot)
        {
            ct.ThrowIfCancellationRequested();
            yield return msg;
            await Task.Yield();
        }
    }

    public Task AckAsync(int topicHash, string subscriberId, long upToSequence, CancellationToken ct = default)
    {
        if (_queues.TryGetValue((topicHash, subscriberId), out var state))
        {
            lock (state.Lock)
            {
                state.Messages.RemoveAll(m => m.Sequence <= upToSequence);
                if (upToSequence > state.LastAckedSequence)
                    state.LastAckedSequence = upToSequence;
            }
        }
        return Task.CompletedTask;
    }

    public Task RegisterDurableSubscriberAsync(int topicHash, string subscriberId, CancellationToken ct = default)
    {
        var set = _subscribers.GetOrAdd(topicHash, _ => new ConcurrentDictionary<string, byte>());
        set.TryAdd(subscriberId, 0);
        // Ensure queue state exists
        _queues.GetOrAdd((topicHash, subscriberId), _ => new QueueState());
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetDurableSubscribersAsync(int topicHash, CancellationToken ct = default)
    {
        if (_subscribers.TryGetValue(topicHash, out var set))
            return Task.FromResult<IReadOnlyList<string>>(set.Keys.ToList());
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    public Task<long> GetLastAckedSequenceAsync(int topicHash, string subscriberId, CancellationToken ct = default)
    {
        if (_queues.TryGetValue((topicHash, subscriberId), out var state))
        {
            lock (state.Lock)
                return Task.FromResult(state.LastAckedSequence);
        }
        return Task.FromResult(0L);
    }

    private sealed class QueueState
    {
        public readonly object Lock = new();
        public long NextSequence;
        public long LastAckedSequence;
        public readonly List<(long Sequence, byte[] Payload)> Messages = new();
    }
}
```

- [ ] **Step 4: Add Bolt.Hub project compilation check**

Run: `dotnet build src/Modules/XFramework.Bolt/Bolt.Hub/Bolt.Hub.csproj`
Expected: Build succeeds with 0 errors. (The new files are not yet wired into anything; they just need to compile.)

- [ ] **Step 5: Write tests for InMemoryDurableQueueStore**

Create `src/Tests/Bolt.Tests/InMemoryDurableQueueStoreTests.cs`:

```csharp
using Bolt.Hub.Durable;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public class InMemoryDurableQueueStoreTests
{
    private InMemoryDurableQueueStore CreateStore(int maxQueueSize = 10_000) =>
        new(Options.Create(new DurableQueueOptions { MaxQueueSize = maxQueueSize }), NullLogger<InMemoryDurableQueueStore>.Instance);

    [Test]
    public async Task Append_AssignsMonotonicSequenceNumbers()
    {
        var store = CreateStore();
        var s1 = await store.AppendAsync(1, "sub-a", new byte[] { 1 });
        var s2 = await store.AppendAsync(1, "sub-a", new byte[] { 2 });
        var s3 = await store.AppendAsync(1, "sub-a", new byte[] { 3 });

        s1.Should().Be(1);
        s2.Should().Be(2);
        s3.Should().Be(3);
    }

    [Test]
    public async Task Append_DifferentSubscribers_HaveIndependentSequences()
    {
        var store = CreateStore();
        var s1 = await store.AppendAsync(1, "sub-a", new byte[] { 1 });
        var s2 = await store.AppendAsync(1, "sub-b", new byte[] { 2 });

        s1.Should().Be(1);
        s2.Should().Be(1);
    }

    [Test]
    public async Task ReadFrom_ReturnsMessagesAfterFromSequence()
    {
        var store = CreateStore();
        await store.AppendAsync(1, "sub-a", new byte[] { 1 });
        await store.AppendAsync(1, "sub-a", new byte[] { 2 });
        await store.AppendAsync(1, "sub-a", new byte[] { 3 });

        var results = new List<(long, byte[])>();
        await foreach (var msg in store.ReadFromAsync(1, "sub-a", fromSequence: 1, maxCount: 100))
            results.Add(msg);

        results.Should().HaveCount(2);
        results[0].Item1.Should().Be(2);
        results[1].Item1.Should().Be(3);
    }

    [Test]
    public async Task Ack_RemovesAckedMessages()
    {
        var store = CreateStore();
        await store.AppendAsync(1, "sub-a", new byte[] { 1 });
        await store.AppendAsync(1, "sub-a", new byte[] { 2 });
        await store.AppendAsync(1, "sub-a", new byte[] { 3 });

        await store.AckAsync(1, "sub-a", upToSequence: 2);

        var results = new List<(long, byte[])>();
        await foreach (var msg in store.ReadFromAsync(1, "sub-a", fromSequence: 0, maxCount: 100))
            results.Add(msg);

        results.Should().HaveCount(1);
        results[0].Item1.Should().Be(3);
    }

    [Test]
    public async Task MaxQueueSize_DropsOldestMessages()
    {
        var store = CreateStore(maxQueueSize: 3);
        await store.AppendAsync(1, "sub-a", new byte[] { 1 });
        await store.AppendAsync(1, "sub-a", new byte[] { 2 });
        await store.AppendAsync(1, "sub-a", new byte[] { 3 });
        await store.AppendAsync(1, "sub-a", new byte[] { 4 });
        await store.AppendAsync(1, "sub-a", new byte[] { 5 });

        var results = new List<(long, byte[])>();
        await foreach (var msg in store.ReadFromAsync(1, "sub-a", fromSequence: 0, maxCount: 100))
            results.Add(msg);

        results.Should().HaveCount(3);
        results.Select(r => r.Item1).Should().BeEquivalentTo(new long[] { 3, 4, 5 });
    }

    [Test]
    public async Task RegisterDurableSubscriber_IsIdempotent()
    {
        var store = CreateStore();
        await store.RegisterDurableSubscriberAsync(1, "sub-a");
        await store.RegisterDurableSubscriberAsync(1, "sub-a");
        await store.RegisterDurableSubscriberAsync(1, "sub-b");

        var subs = await store.GetDurableSubscribersAsync(1);
        subs.Should().BeEquivalentTo(new[] { "sub-a", "sub-b" });
    }

    [Test]
    public async Task GetLastAckedSequence_ReturnsZeroForUnknownSubscriber()
    {
        var store = CreateStore();
        var seq = await store.GetLastAckedSequenceAsync(1, "unknown");
        seq.Should().Be(0);
    }

    [Test]
    public async Task GetLastAckedSequence_ReturnsLastAcked()
    {
        var store = CreateStore();
        await store.AppendAsync(1, "sub-a", new byte[] { 1 });
        await store.AppendAsync(1, "sub-a", new byte[] { 2 });
        await store.AckAsync(1, "sub-a", upToSequence: 2);

        var seq = await store.GetLastAckedSequenceAsync(1, "sub-a");
        seq.Should().Be(2);
    }
}
```

- [ ] **Step 6: Add project reference to test project (if missing)**

Open `src/Tests/Bolt.Tests/Bolt.Tests.csproj` and verify it references `Bolt.Hub.csproj`. If not, add:

```xml
<ProjectReference Include="..\..\Modules\XFramework.Bolt\Bolt.Hub\Bolt.Hub.csproj" />
```

- [ ] **Step 7: Run tests**

Run: `dotnet test src/Tests/Bolt.Tests/Bolt.Tests.csproj --filter "FullyQualifiedName~InMemoryDurableQueueStoreTests"`
Expected: 8 tests pass.

- [ ] **Step 8: Commit**

```bash
git add src/Modules/XFramework.Bolt/Bolt.Hub/Durable/ src/Tests/Bolt.Tests/InMemoryDurableQueueStoreTests.cs src/Tests/Bolt.Tests/Bolt.Tests.csproj
git commit -m "feat(bolt-hub): IDurableQueueStore + InMemoryDurableQueueStore implementation"
```

---

## Task 4: Redis Durable Queue Store

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `src/Modules/XFramework.Bolt/Bolt.Hub/Bolt.Hub.csproj`
- Create: `src/Modules/XFramework.Bolt/Bolt.Hub/Durable/RedisDurableQueueStore.cs`

### Context for implementer

`StackExchange.Redis` is the standard .NET Redis client. We use Redis Streams (`XADD` / `XREAD` / `XTRIM` / `XLEN` / `XRANGE`) for the message queues, plus Redis Sets (`SADD` / `SMEMBERS`) for the subscriber registry.

Key naming convention:
- Stream: `bolt:durable:msg:{topicHash}:{subscriberId}` — XADD/XRANGE for messages
- Set: `bolt:durable:subs:{topicHash}` — SMEMBERS for registered durable subscribers
- Hash: `bolt:durable:ack:{topicHash}:{subscriberId}` — last acked sequence (used to compute starting point)

We assign our own monotonic sequence numbers (using `INCR` on `bolt:durable:seq:{topicHash}:{subscriberId}`) and store them in the stream entries. We don't rely on Redis's native stream IDs because we want explicit control over ordering across reconnects.

- [ ] **Step 1: Add StackExchange.Redis to Directory.Packages.props**

Open `Directory.Packages.props` and add (in the appropriate alphabetized location):

```xml
<PackageVersion Include="StackExchange.Redis" Version="2.8.16" />
```

- [ ] **Step 2: Add StackExchange.Redis to Bolt.Hub.csproj**

Open `src/Modules/XFramework.Bolt/Bolt.Hub/Bolt.Hub.csproj` and add in the `<ItemGroup>` containing `<PackageReference>` entries:

```xml
<PackageReference Include="StackExchange.Redis" />
```

- [ ] **Step 3: Create RedisDurableQueueStore**

Create `src/Modules/XFramework.Bolt/Bolt.Hub/Durable/RedisDurableQueueStore.cs`:

```csharp
using System.Buffers.Binary;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Bolt.Hub.Durable;

/// <summary>
/// Redis-backed durable queue store using Redis Streams.
///
/// Key conventions:
/// - bolt:durable:msg:{topicHash}:{subscriberId}  (stream)  — message queue
/// - bolt:durable:subs:{topicHash}                (set)     — registered subscriberIds
/// - bolt:durable:seq:{topicHash}:{subscriberId}  (string)  — monotonic counter
/// - bolt:durable:ack:{topicHash}:{subscriberId}  (string)  — last acked sequence
/// </summary>
public sealed class RedisDurableQueueStore : IDurableQueueStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly DurableQueueOptions _options;
    private readonly ILogger<RedisDurableQueueStore> _logger;

    public RedisDurableQueueStore(IConnectionMultiplexer redis, IOptions<DurableQueueOptions> options, ILogger<RedisDurableQueueStore> logger)
    {
        _redis = redis;
        _options = options.Value;
        _logger = logger;
    }

    private static string MsgKey(int topicHash, string subscriberId) => $"bolt:durable:msg:{topicHash}:{subscriberId}";
    private static string SubsKey(int topicHash) => $"bolt:durable:subs:{topicHash}";
    private static string SeqKey(int topicHash, string subscriberId) => $"bolt:durable:seq:{topicHash}:{subscriberId}";
    private static string AckKey(int topicHash, string subscriberId) => $"bolt:durable:ack:{topicHash}:{subscriberId}";

    public async Task<long> AppendAsync(int topicHash, string subscriberId, ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var seq = await db.StringIncrementAsync(SeqKey(topicHash, subscriberId));

        var seqBytes = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(seqBytes, seq);

        await db.StreamAddAsync(
            MsgKey(topicHash, subscriberId),
            new NameValueEntry[]
            {
                new("seq", seqBytes),
                new("payload", payload.ToArray())
            },
            maxLength: _options.MaxQueueSize,
            useApproximateMaxLength: true);

        return seq;
    }

    public async IAsyncEnumerable<(long Sequence, byte[] Payload)> ReadFromAsync(int topicHash, string subscriberId, long fromSequence, int maxCount, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var entries = await db.StreamRangeAsync(MsgKey(topicHash, subscriberId), count: maxCount);

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();

            var seqValue = entry.Values.FirstOrDefault(v => v.Name == "seq").Value;
            var payloadValue = entry.Values.FirstOrDefault(v => v.Name == "payload").Value;
            if (seqValue.IsNullOrEmpty || payloadValue.IsNullOrEmpty) continue;

            var seqBytes = (byte[])seqValue!;
            var seq = BinaryPrimitives.ReadInt64LittleEndian(seqBytes);
            if (seq <= fromSequence) continue;

            yield return (seq, (byte[])payloadValue!);
        }
    }

    public async Task AckAsync(int topicHash, string subscriberId, long upToSequence, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();

        // Update last acked
        await db.StringSetAsync(AckKey(topicHash, subscriberId), upToSequence);

        // Delete entries with seq <= upToSequence
        var entries = await db.StreamRangeAsync(MsgKey(topicHash, subscriberId));
        var toDelete = new List<RedisValue>();
        foreach (var entry in entries)
        {
            var seqValue = entry.Values.FirstOrDefault(v => v.Name == "seq").Value;
            if (seqValue.IsNullOrEmpty) continue;
            var seq = BinaryPrimitives.ReadInt64LittleEndian((byte[])seqValue!);
            if (seq <= upToSequence)
                toDelete.Add(entry.Id);
        }
        if (toDelete.Count > 0)
            await db.StreamDeleteAsync(MsgKey(topicHash, subscriberId), toDelete.ToArray());
    }

    public async Task RegisterDurableSubscriberAsync(int topicHash, string subscriberId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.SetAddAsync(SubsKey(topicHash), subscriberId);
    }

    public async Task<IReadOnlyList<string>> GetDurableSubscribersAsync(int topicHash, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var members = await db.SetMembersAsync(SubsKey(topicHash));
        return members.Select(m => m.ToString()).ToList();
    }

    public async Task<long> GetLastAckedSequenceAsync(int topicHash, string subscriberId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(AckKey(topicHash, subscriberId));
        return value.IsNullOrEmpty ? 0L : (long)value;
    }
}
```

- [ ] **Step 4: Verify build**

Run: `dotnet build src/Modules/XFramework.Bolt/Bolt.Hub/Bolt.Hub.csproj`
Expected: Build succeeds.

- [ ] **Step 5: Commit**

```bash
git add Directory.Packages.props src/Modules/XFramework.Bolt/Bolt.Hub/Bolt.Hub.csproj src/Modules/XFramework.Bolt/Bolt.Hub/Durable/RedisDurableQueueStore.cs
git commit -m "feat(bolt-hub): RedisDurableQueueStore using Redis Streams"
```

---

## Task 5: BoltClient Pub/Sub API (Transient + Durable)

**Files:**
- Create: `src/Libraries/Bolt/Bolt.Client/DurableMessage.cs`
- Modify: `src/Libraries/Bolt/Bolt.Client/BoltClient.cs`

### Context for implementer

`BoltClient` already has frame dispatch (via `RegisterFrameHandler`) and a connection pool (`GetPrimaryConnection()`). We need to add four public methods plus reconnect handling.

The client maintains two dictionaries:
- `_transientSubscriptions`: `topicHash` → `Channel<byte[]>` for live deliveries
- `_durableSubscriptions`: `(topicHash, subscriberId)` → `Channel<DurableMessage<byte[]>>` for durable deliveries

When the `Event` frame arrives (frame type 0x09), it's dispatched to the matching channel. Reconnection logic re-sends `Subscribe` frames for all active subscriptions.

`BoltClient` is in `Bolt.Client/BoltClient.cs`. Subscribers iterate via `IAsyncEnumerable<T>` backed by a `Channel<T>`.

- [ ] **Step 1: Create DurableMessage.cs**

Create `src/Libraries/Bolt/Bolt.Client/DurableMessage.cs`:

```csharp
namespace Bolt.Client;

/// <summary>
/// Wraps a durable message payload with its sequence number and replay flag.
/// Carries an Ack helper that calls back into the originating BoltClient.
/// </summary>
public sealed class DurableMessage<T>
{
    private readonly Func<long, CancellationToken, ValueTask> _ackCallback;

    public T Payload { get; }
    public long Sequence { get; }
    public bool IsReplay { get; }

    internal DurableMessage(T payload, long sequence, bool isReplay, Func<long, CancellationToken, ValueTask> ackCallback)
    {
        Payload = payload;
        Sequence = sequence;
        IsReplay = isReplay;
        _ackCallback = ackCallback;
    }

    /// <summary>Acknowledge this message (and all earlier ones from the same subscriber).</summary>
    public ValueTask AckAsync(CancellationToken ct = default) => _ackCallback(Sequence, ct);
}
```

- [ ] **Step 2: Add subscription state fields to BoltClient**

Open `src/Libraries/Bolt/Bolt.Client/BoltClient.cs`. Find the field declarations near the top of the class (where `_logger`, `_options`, etc. are declared) and add:

```csharp
    // Pub/sub state
    private readonly ConcurrentDictionary<int, TransientSubscription> _transientSubscriptions = new();
    private readonly ConcurrentDictionary<(int TopicHash, string SubscriberId), DurableSubscription> _durableSubscriptions = new();

    private sealed class TransientSubscription
    {
        public required string Topic { get; init; }
        public required Channel<byte[]> Channel { get; init; }
    }

    private sealed class DurableSubscription
    {
        public required string Topic { get; init; }
        public required string SubscriberId { get; init; }
        public required Channel<(long Sequence, bool IsReplay, byte[] Payload)> Channel { get; init; }
    }
```

If `Channel` is not yet imported, add `using System.Threading.Channels;` to the file's using directives.

- [ ] **Step 3: Register Event frame handler in BoltClient constructor**

Find the `BoltClient` constructor in `src/Libraries/Bolt/Bolt.Client/BoltClient.cs`. After the existing field initializations (and before the constructor body ends), add:

```csharp
        // Wire pub/sub Event frame dispatch
        RegisterFrameHandler(FrameType.Event, HandleEventFrame);
```

If `RegisterFrameHandler` is called from inside the constructor and needs the connection, ensure it's called after connection setup. Place it just before the constructor's closing brace.

- [ ] **Step 4: Add HandleEventFrame method**

In `BoltClient.cs`, add a new private method (anywhere reasonable in the class — near other frame handlers if any, or at the bottom):

```csharp
    private void HandleEventFrame(BoltConnection conn, byte[] buffer, int length)
    {
        if (!BoltCodec.TryReadEvent(buffer.AsSpan(0, length), out var topicHash, out var sequence, out var isReplay, out var payloadOffset, out var payloadLength, out _))
            return;

        var payload = new byte[payloadLength];
        buffer.AsSpan(payloadOffset, payloadLength).CopyTo(payload);

        // Try transient first
        if (_transientSubscriptions.TryGetValue(topicHash, out var transient))
        {
            transient.Channel.Writer.TryWrite(payload);
            return;
        }

        // Try durable: there may be multiple durable subscriptions for the same topic with different subscriberIds
        // The client typically only registers one subscriberId per topic, but support multiple just in case
        foreach (var kvp in _durableSubscriptions)
        {
            if (kvp.Key.TopicHash == topicHash)
                kvp.Value.Channel.Writer.TryWrite((sequence, isReplay, payload));
        }
    }
```

- [ ] **Step 5: Add SubscribeAsync (transient)**

Add a new public method to `BoltClient`:

```csharp
    /// <summary>
    /// Subscribe to a topic. Receives published messages as they arrive (transient — no persistence, no replay).
    /// Cancelling the cancellation token unsubscribes.
    /// </summary>
    public async IAsyncEnumerable<T> SubscribeAsync<T>(string topic, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var topicHash = BoltCodec.Fnv1aHash(topic);
        var channel = Channel.CreateUnbounded<byte[]>();
        var sub = new TransientSubscription { Topic = topic, Channel = channel };

        if (!_transientSubscriptions.TryAdd(topicHash, sub))
            throw new InvalidOperationException($"Already subscribed to topic '{topic}'");

        // Send Subscribe frame
        var conn = GetPrimaryConnection();
        var writer = Bolt.Protocol.Buffers.RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteSubscribe(writer, topic, _clientId, durable: false);
        await conn.SendAsync(writer.WrittenMemory, ct);
        writer.Reset();

        try
        {
            while (await channel.Reader.WaitToReadAsync(ct))
            {
                while (channel.Reader.TryRead(out var payload))
                {
                    var item = MemoryPack.MemoryPackSerializer.Deserialize<T>(payload);
                    if (item is not null) yield return item;
                }
            }
        }
        finally
        {
            // Unsubscribe on cancellation
            _transientSubscriptions.TryRemove(topicHash, out _);
            try
            {
                var w = Bolt.Protocol.Buffers.RentedBufferWriter.GetThreadLocal();
                BoltCodec.WriteUnsubscribe(w, topic, _clientId);
                await conn.SendAsync(w.WrittenMemory, CancellationToken.None);
                w.Reset();
            }
            catch { /* best-effort */ }
        }
    }
```

- [ ] **Step 6: Add SubscribeDurableAsync**

Add to `BoltClient`:

```csharp
    /// <summary>
    /// Subscribe to a topic durably. On reconnect, queued messages are replayed.
    /// Each message must be acked via DurableMessage.AckAsync to prevent re-delivery.
    /// </summary>
    public async IAsyncEnumerable<DurableMessage<T>> SubscribeDurableAsync<T>(string topic, string subscriberId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var topicHash = BoltCodec.Fnv1aHash(topic);
        var key = (topicHash, subscriberId);
        var channel = Channel.CreateUnbounded<(long, bool, byte[])>();
        var sub = new DurableSubscription { Topic = topic, SubscriberId = subscriberId, Channel = channel };

        if (!_durableSubscriptions.TryAdd(key, sub))
            throw new InvalidOperationException($"Already subscribed to topic '{topic}' with subscriberId '{subscriberId}'");

        // Send Subscribe frame with durable=true
        var conn = GetPrimaryConnection();
        var writer = Bolt.Protocol.Buffers.RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteSubscribe(writer, topic, subscriberId, durable: true);
        await conn.SendAsync(writer.WrittenMemory, ct);
        writer.Reset();

        try
        {
            while (await channel.Reader.WaitToReadAsync(ct))
            {
                while (channel.Reader.TryRead(out var entry))
                {
                    var (seq, isReplay, payload) = entry;
                    var item = MemoryPack.MemoryPackSerializer.Deserialize<T>(payload);
                    if (item is null) continue;

                    yield return new DurableMessage<T>(item, seq, isReplay, async (s, c) =>
                    {
                        await AckAsync(topic, subscriberId, s, c);
                    });
                }
            }
        }
        finally
        {
            _durableSubscriptions.TryRemove(key, out _);
            try
            {
                var w = Bolt.Protocol.Buffers.RentedBufferWriter.GetThreadLocal();
                BoltCodec.WriteUnsubscribe(w, topic, subscriberId);
                await conn.SendAsync(w.WrittenMemory, CancellationToken.None);
                w.Reset();
            }
            catch { /* best-effort */ }
        }
    }
```

- [ ] **Step 7: Add PublishAsync**

Add to `BoltClient`:

```csharp
    /// <summary>
    /// Publish a message to a topic. If durable=true, the Hub queues the message for any
    /// currently-registered durable subscribers (so offline subscribers receive it on reconnect).
    /// If durable=false, the message is fan-out only.
    /// </summary>
    public async ValueTask PublishAsync<T>(string topic, T payload, bool durable = false, CancellationToken ct = default)
    {
        var bytes = MemoryPack.MemoryPackSerializer.Serialize(payload);
        var conn = GetPrimaryConnection();
        var writer = Bolt.Protocol.Buffers.RentedBufferWriter.GetThreadLocal();
        BoltCodec.WritePublish(writer, topic, durable, bytes);
        await conn.SendAsync(writer.WrittenMemory, ct);
        writer.Reset();
    }
```

- [ ] **Step 8: Add AckAsync**

Add to `BoltClient`:

```csharp
    /// <summary>
    /// Acknowledge durable messages up to and including upToSequence for a (topic, subscriber) pair.
    /// </summary>
    public async ValueTask AckAsync(string topic, string subscriberId, long upToSequence, CancellationToken ct = default)
    {
        var topicHash = BoltCodec.Fnv1aHash(topic);
        var conn = GetPrimaryConnection();
        var writer = Bolt.Protocol.Buffers.RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteAck(writer, topicHash, subscriberId, upToSequence);
        await conn.SendAsync(writer.WrittenMemory, ct);
        writer.Reset();
    }
```

- [ ] **Step 9: Add subscription re-send on reconnect**

Find the reconnection logic in `BoltClient.cs` (look for `ConnectWithRetryAsync` or `ReconnectAsync`). After a successful reconnection — specifically after the new connection has been registered — re-send all active Subscribe frames. Add this code after the registration step in the reconnect handler:

```csharp
        // Re-send all active subscriptions
        foreach (var (_, sub) in _transientSubscriptions)
        {
            try
            {
                var w = Bolt.Protocol.Buffers.RentedBufferWriter.GetThreadLocal();
                BoltCodec.WriteSubscribe(w, sub.Topic, _clientId, durable: false);
                await GetPrimaryConnection().SendAsync(w.WrittenMemory, CancellationToken.None);
                w.Reset();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to re-send transient subscription for topic {Topic}", sub.Topic);
            }
        }

        foreach (var (_, sub) in _durableSubscriptions)
        {
            try
            {
                var w = Bolt.Protocol.Buffers.RentedBufferWriter.GetThreadLocal();
                BoltCodec.WriteSubscribe(w, sub.Topic, sub.SubscriberId, durable: true);
                await GetPrimaryConnection().SendAsync(w.WrittenMemory, CancellationToken.None);
                w.Reset();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to re-send durable subscription for topic {Topic} subscriber {SubscriberId}", sub.Topic, sub.SubscriberId);
            }
        }
```

Note: If `BoltClient` doesn't have an explicit reconnect hook (e.g., if it uses the negotiator's auto-reconnect inside `IBoltConnection`), expose a connection-established event and subscribe to it. If that's complex, an alternative is to track subscriptions and re-send them in the `EnsureConnection` path. The implementer should pick the cleanest spot consistent with the existing code.

- [ ] **Step 10: Verify build**

Run: `dotnet build src/Libraries/Bolt/Bolt.Client/Bolt.Client.csproj`
Expected: Build succeeds.

- [ ] **Step 11: Commit**

```bash
git add src/Libraries/Bolt/Bolt.Client/DurableMessage.cs src/Libraries/Bolt/Bolt.Client/BoltClient.cs
git commit -m "feat(bolt-client): SubscribeAsync, SubscribeDurableAsync, PublishAsync, AckAsync + reconnect re-subscribe"
```

---

## Task 6: BoltServer Pub/Sub Frame Handlers

**Files:**
- Modify: `src/Modules/XFramework.Bolt/Bolt.Hub/ThinProtocol/BoltServer.cs`

### Context for implementer

`BoltServer` is the Hub-side WebSocket server. It already has a frame dispatch loop. We add handlers for `Subscribe`, `Unsubscribe`, `Publish`, and `Ack`. Pub/sub state is tracked in fields on `BoltServer`. Durable persistence is delegated to `IDurableQueueStore`.

The Hub injection point for `IDurableQueueStore` is via `BoltServer`'s constructor — add it as a parameter and update the DI registration.

- [ ] **Step 1: Add pub/sub state fields to BoltServer**

Open `src/Modules/XFramework.Bolt/Bolt.Hub/ThinProtocol/BoltServer.cs`. Add field declarations near the top of the class:

```csharp
    // Pub/sub state — transient (live fan-out only)
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<BoltHubConnection, byte>> _liveSubscribersByTopic = new();
    private readonly ConcurrentDictionary<BoltHubConnection, ConcurrentDictionary<int, byte>> _liveSubscriptionsByConnection = new();

    // Pub/sub state — durable (persistent identity)
    // Maps (topicHash, subscriberId) → currently-connected connection (if any)
    private readonly ConcurrentDictionary<(int TopicHash, string SubscriberId), BoltHubConnection> _liveDurableConnections = new();

    // Durable queue backend
    private readonly Bolt.Hub.Durable.IDurableQueueStore _durableStore;
    private readonly Bolt.Hub.Durable.DurableQueueOptions _durableOptions;
```

If `ConcurrentDictionary` is not yet imported, add `using System.Collections.Concurrent;`.

- [ ] **Step 2: Inject IDurableQueueStore via constructor**

Find the existing `BoltServer` constructor. Update it to accept `IDurableQueueStore` and `IOptions<DurableQueueOptions>`:

```csharp
    public BoltServer(
        ILogger<BoltServer> logger,
        Bolt.Hub.Durable.IDurableQueueStore durableStore,
        Microsoft.Extensions.Options.IOptions<Bolt.Hub.Durable.DurableQueueOptions> durableOptions)
    {
        _logger = logger;
        _durableStore = durableStore;
        _durableOptions = durableOptions.Value;
    }
```

(Preserve any existing constructor body — only add the new parameters and assignments.)

- [ ] **Step 3: Add HandleSubscribeFrame method**

Add a new method to `BoltServer`:

```csharp
    private async Task HandleSubscribeFrameAsync(BoltHubConnection conn, byte[] buffer, int length, CancellationToken ct)
    {
        if (!BoltCodec.TryReadSubscribe(buffer.AsSpan(0, length), out var topicHash, out var durable, out var subscriberId, out var topic, out _))
            return;

        // Add to live subscribers
        var topicSet = _liveSubscribersByTopic.GetOrAdd(topicHash, _ => new ConcurrentDictionary<BoltHubConnection, byte>());
        topicSet.TryAdd(conn, 0);

        var connSet = _liveSubscriptionsByConnection.GetOrAdd(conn, _ => new ConcurrentDictionary<int, byte>());
        connSet.TryAdd(topicHash, 0);

        if (!durable)
        {
            _logger.LogDebug("Transient subscribe: topic={Topic} client={Client}", topic, conn.ClientName);
            return;
        }

        // Durable: register subscriber, set live mapping, replay queued messages
        await _durableStore.RegisterDurableSubscriberAsync(topicHash, subscriberId, ct);
        _liveDurableConnections[(topicHash, subscriberId)] = conn;

        var lastAcked = await _durableStore.GetLastAckedSequenceAsync(topicHash, subscriberId, ct);
        var replayCount = 0;
        await foreach (var (seq, payload) in _durableStore.ReadFromAsync(topicHash, subscriberId, lastAcked, _durableOptions.MaxReplayBatchSize, ct))
        {
            var w = Bolt.Protocol.Buffers.RentedBufferWriter.GetThreadLocal();
            BoltCodec.WriteEvent(w, topicHash, seq, isReplay: true, payload);
            await conn.SendAsync(w.WrittenMemory, ct);
            w.Reset();
            replayCount++;
        }

        _logger.LogDebug("Durable subscribe: topic={Topic} subscriber={Subscriber} replayed={Count}", topic, subscriberId, replayCount);
    }
```

- [ ] **Step 4: Add HandleUnsubscribeFrame method**

```csharp
    private void HandleUnsubscribeFrame(BoltHubConnection conn, byte[] buffer, int length)
    {
        if (!BoltCodec.TryReadUnsubscribe(buffer.AsSpan(0, length), out var topicHash, out var subscriberId, out _))
            return;

        if (_liveSubscribersByTopic.TryGetValue(topicHash, out var topicSet))
            topicSet.TryRemove(conn, out _);

        if (_liveSubscriptionsByConnection.TryGetValue(conn, out var connSet))
            connSet.TryRemove(topicHash, out _);

        _liveDurableConnections.TryRemove((topicHash, subscriberId), out _);

        _logger.LogDebug("Unsubscribe: topicHash={TopicHash} subscriber={Subscriber}", topicHash, subscriberId);
    }
```

- [ ] **Step 5: Add HandlePublishFrame method**

```csharp
    private async Task HandlePublishFrameAsync(BoltHubConnection publisher, byte[] buffer, int length, CancellationToken ct)
    {
        if (!BoltCodec.TryReadPublish(buffer.AsSpan(0, length), out var topicHash, out var durableEligible, out var payloadOffset, out var payloadLength, out _))
            return;

        var payload = new byte[payloadLength];
        buffer.AsSpan(payloadOffset, payloadLength).CopyTo(payload);

        // Track which connections we've already delivered to (durable connections override live fan-out)
        var deliveredConnections = new HashSet<BoltHubConnection>();

        // Durable path: enqueue for each registered durable subscriber and deliver live if connected
        if (durableEligible)
        {
            var durableSubs = await _durableStore.GetDurableSubscribersAsync(topicHash, ct);
            foreach (var subscriberId in durableSubs)
            {
                long seq;
                try
                {
                    seq = await _durableStore.AppendAsync(topicHash, subscriberId, payload, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Durable append failed for topic={TopicHash} subscriber={Subscriber}", topicHash, subscriberId);
                    continue;
                }

                if (_liveDurableConnections.TryGetValue((topicHash, subscriberId), out var liveConn) && liveConn != publisher)
                {
                    var w = Bolt.Protocol.Buffers.RentedBufferWriter.GetThreadLocal();
                    BoltCodec.WriteEvent(w, topicHash, seq, isReplay: false, payload);
                    try { await liveConn.SendAsync(w.WrittenMemory, ct); }
                    catch { /* connection may have dropped */ }
                    w.Reset();
                    deliveredConnections.Add(liveConn);
                }
            }
        }

        // Live fan-out for transient subscribers (skip publisher and skip durable-already-delivered)
        if (_liveSubscribersByTopic.TryGetValue(topicHash, out var topicSet))
        {
            foreach (var (subscriberConn, _) in topicSet)
            {
                if (subscriberConn == publisher) continue;
                if (deliveredConnections.Contains(subscriberConn)) continue;

                var w = Bolt.Protocol.Buffers.RentedBufferWriter.GetThreadLocal();
                BoltCodec.WriteEvent(w, topicHash, sequenceNumber: 0, isReplay: false, payload);
                try { await subscriberConn.SendAsync(w.WrittenMemory, ct); }
                catch { /* connection may have dropped */ }
                w.Reset();
            }
        }
    }
```

- [ ] **Step 6: Add HandleAckFrame method**

```csharp
    private async Task HandleAckFrameAsync(BoltHubConnection conn, byte[] buffer, int length, CancellationToken ct)
    {
        if (!BoltCodec.TryReadAck(buffer.AsSpan(0, length), out var topicHash, out var subscriberId, out var upToSequence, out _))
            return;

        try
        {
            await _durableStore.AckAsync(topicHash, subscriberId, upToSequence, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Durable ack failed for topic={TopicHash} subscriber={Subscriber}", topicHash, subscriberId);
        }
    }
```

- [ ] **Step 7: Wire frame dispatch**

Find the frame dispatch switch in `BoltServer.cs` (the place where it reads `frameType` and dispatches based on it). Add cases for the new frame types:

```csharp
            case FrameType.Subscribe:
                await HandleSubscribeFrameAsync(connection, buffer, bytesRead, ct);
                break;
            case FrameType.Unsubscribe:
                HandleUnsubscribeFrame(connection, buffer, bytesRead);
                break;
            case FrameType.Publish:
                await HandlePublishFrameAsync(connection, buffer, bytesRead, ct);
                break;
            case FrameType.Ack:
                await HandleAckFrameAsync(connection, buffer, bytesRead, ct);
                break;
```

- [ ] **Step 8: Clean up subscriptions on disconnect**

Find the connection cleanup logic in `BoltServer.cs` (where the receive loop handles the connection closing). Add subscription cleanup:

```csharp
        // Clean up pub/sub subscriptions for this connection
        if (_liveSubscriptionsByConnection.TryRemove(connection, out var topics))
        {
            foreach (var (topicHash, _) in topics)
            {
                if (_liveSubscribersByTopic.TryGetValue(topicHash, out var topicSet))
                    topicSet.TryRemove(connection, out _);
            }
        }

        // Remove this connection from any live durable bindings
        var keysToRemove = _liveDurableConnections.Where(kvp => kvp.Value == connection).Select(kvp => kvp.Key).ToList();
        foreach (var key in keysToRemove)
            _liveDurableConnections.TryRemove(key, out _);
```

- [ ] **Step 9: Register IDurableQueueStore in BoltInstaller**

Open `src/Modules/XFramework.Bolt/Bolt.Hub/Installers/BoltInstaller.cs`. In the `InstallServices` method (or wherever services are registered), add:

```csharp
        // Durable queue store (Redis if configured, in-memory fallback)
        services.Configure<Bolt.Hub.Durable.DurableQueueOptions>(configuration.GetSection("BoltConfiguration:Durable"));
        var redisConn = configuration["BoltConfiguration:Durable:RedisConnectionString"];
        if (!string.IsNullOrWhiteSpace(redisConn))
        {
            services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(_ =>
                StackExchange.Redis.ConnectionMultiplexer.Connect(redisConn));
            services.AddSingleton<Bolt.Hub.Durable.IDurableQueueStore, Bolt.Hub.Durable.RedisDurableQueueStore>();
        }
        else
        {
            services.AddSingleton<Bolt.Hub.Durable.IDurableQueueStore, Bolt.Hub.Durable.InMemoryDurableQueueStore>();
        }
```

Place this near the existing `BoltServer` registration so they're together.

- [ ] **Step 10: Verify build**

Run: `dotnet build src/Modules/XFramework.Bolt/Bolt.Hub/Bolt.Hub.csproj`
Expected: Build succeeds.

- [ ] **Step 11: Commit**

```bash
git add src/Modules/XFramework.Bolt/Bolt.Hub/ThinProtocol/BoltServer.cs src/Modules/XFramework.Bolt/Bolt.Hub/Installers/BoltInstaller.cs
git commit -m "feat(bolt-hub): pub/sub frame handlers (transient + durable) in BoltServer"
```

---

## Task 7: Pub/Sub Integration Tests

**Files:**
- Create: `src/Tests/Bolt.Tests/BoltPubSubIntegrationTests.cs`

### Context for implementer

These tests spin up a real `BoltServer` + `BoltClient` (in-process WebSocket) and verify the pub/sub flow end-to-end. The tests use the `InMemoryDurableQueueStore` (no Redis required for CI). Use the existing test infrastructure pattern from `TransportTests.cs` (which already starts an in-process Bolt hub for testing).

- [ ] **Step 1: Read existing TransportTests.cs as a reference**

Open `src/Tests/Bolt.Tests/TransportTests.cs` and review the test setup pattern. The new tests follow the same shape: start an `IHost` with `BoltServer` + `MapBolt`, create one or more `BoltClient` instances pointing at the test server, exercise the API, assert outcomes.

- [ ] **Step 2: Create BoltPubSubIntegrationTests.cs**

Create `src/Tests/Bolt.Tests/BoltPubSubIntegrationTests.cs`:

```csharp
using System.Threading.Channels;
using Bolt.Client;
using Bolt.Hub.Durable;
using Bolt.Protocol;
using FluentAssertions;
using MemoryPack;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Bolt.Tests;

[MemoryPackable]
public partial record TestMessage(int Id, string Text);

[TestFixture]
public class BoltPubSubIntegrationTests
{
    private IHost _host = null!;
    private Uri _serverUri = null!;
    private const int Port = 5891;

    [OneTimeSetUp]
    public async Task SetUp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{Port}");
        builder.Services.AddSingleton<IDurableQueueStore>(_ =>
            new InMemoryDurableQueueStore(Options.Create(new DurableQueueOptions { MaxQueueSize = 100, MaxReplayBatchSize = 100 }), NullLogger<InMemoryDurableQueueStore>.Instance));
        builder.Services.Configure<DurableQueueOptions>(_ => { });
        builder.Services.AddSingleton<Bolt.Server.BoltServer>();
        var app = builder.Build();
        app.UseWebSockets();
        Bolt.Server.BoltMiddleware.MapBolt(app, "/bolt/ws");
        _host = app;
        _ = _host.StartAsync();
        await Task.Delay(500);  // Give server time to bind
        _serverUri = new Uri($"ws://localhost:{Port}/bolt/ws");
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        await _host.StopAsync();
        _host.Dispose();
    }

    private BoltClient CreateClient(string clientId, string clientName)
    {
        var client = new BoltClient(
            _serverUri,
            clientId,
            clientName,
            new BoltClientOptions { RpcTimeoutSeconds = 5 },
            NullLogger<BoltClient>.Instance);
        return client;
    }

    [Test]
    public async Task TransientPubSub_BasicFlow_SubscriberReceivesPublishedMessage()
    {
        var publisher = CreateClient("pub-1", "Publisher");
        var subscriber = CreateClient("sub-1", "Subscriber");

        await publisher.ConnectAsync();
        await subscriber.ConnectAsync();

        var received = new List<TestMessage>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var subTask = Task.Run(async () =>
        {
            await foreach (var msg in subscriber.SubscribeAsync<TestMessage>("test.topic.basic", cts.Token))
            {
                received.Add(msg);
                if (received.Count >= 1) break;
            }
        });

        await Task.Delay(200);  // Let subscription settle
        await publisher.PublishAsync("test.topic.basic", new TestMessage(1, "hello"));

        await subTask;
        received.Should().HaveCount(1);
        received[0].Id.Should().Be(1);
        received[0].Text.Should().Be("hello");

        await publisher.DisposeAsync();
        await subscriber.DisposeAsync();
    }

    [Test]
    public async Task TransientPubSub_PublisherDoesNotReceiveOwnMessages()
    {
        var client = CreateClient("self-pub", "SelfPublisher");
        await client.ConnectAsync();

        var received = new List<TestMessage>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var subTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var msg in client.SubscribeAsync<TestMessage>("test.topic.echo", cts.Token))
                    received.Add(msg);
            }
            catch (OperationCanceledException) { }
        });

        await Task.Delay(200);
        await client.PublishAsync("test.topic.echo", new TestMessage(99, "should-not-receive"));

        cts.CancelAfter(TimeSpan.FromMilliseconds(500));
        try { await subTask; } catch { }

        received.Should().BeEmpty("publisher should not receive its own messages");

        await client.DisposeAsync();
    }

    [Test]
    public async Task DurablePubSub_OfflineMessagesQueued_AndReplayedOnReconnect()
    {
        // Phase 1: subscriber registers durable, then disconnects
        var subscriber1 = CreateClient("sub-durable", "DurableSub");
        await subscriber1.ConnectAsync();

        var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var subTask1 = Task.Run(async () =>
        {
            try
            {
                await foreach (var msg in subscriber1.SubscribeDurableAsync<TestMessage>("test.topic.durable", "subscriber-id-x", cts1.Token))
                {
                    // Just register; do not consume yet
                }
            }
            catch (OperationCanceledException) { }
        });
        await Task.Delay(300);  // Let subscription settle

        cts1.Cancel();
        try { await subTask1; } catch { }
        await subscriber1.DisposeAsync();

        // Phase 2: publisher publishes durable messages while subscriber is offline
        var publisher = CreateClient("pub-durable", "DurablePub");
        await publisher.ConnectAsync();
        await publisher.PublishAsync("test.topic.durable", new TestMessage(1, "msg-1"), durable: true);
        await publisher.PublishAsync("test.topic.durable", new TestMessage(2, "msg-2"), durable: true);
        await publisher.PublishAsync("test.topic.durable", new TestMessage(3, "msg-3"), durable: true);
        await Task.Delay(200);
        await publisher.DisposeAsync();

        // Phase 3: subscriber reconnects with same subscriberId, expects replay
        var subscriber2 = CreateClient("sub-durable-2", "DurableSubReconnect");
        await subscriber2.ConnectAsync();

        var received = new List<DurableMessage<TestMessage>>();
        var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var subTask2 = Task.Run(async () =>
        {
            await foreach (var msg in subscriber2.SubscribeDurableAsync<TestMessage>("test.topic.durable", "subscriber-id-x", cts2.Token))
            {
                received.Add(msg);
                await msg.AckAsync(cts2.Token);
                if (received.Count >= 3) break;
            }
        });

        await subTask2;

        received.Should().HaveCount(3);
        received[0].Payload.Id.Should().Be(1);
        received[1].Payload.Id.Should().Be(2);
        received[2].Payload.Id.Should().Be(3);
        received.Should().AllSatisfy(m => m.IsReplay.Should().BeTrue());

        await subscriber2.DisposeAsync();
    }

    [Test]
    public async Task DurablePubSub_AckTrimsQueue_NoReplayAfterAck()
    {
        // Subscriber registers, gets messages, acks them all, disconnects, reconnects → no replay
        var sub1 = CreateClient("sub-ack-1", "AckSub");
        await sub1.ConnectAsync();

        var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var subTask1 = Task.Run(async () =>
        {
            try
            {
                await foreach (var msg in sub1.SubscribeDurableAsync<TestMessage>("test.topic.ack", "ack-sub-id", cts1.Token))
                { }
            }
            catch (OperationCanceledException) { }
        });
        await Task.Delay(300);
        cts1.Cancel();
        try { await subTask1; } catch { }
        await sub1.DisposeAsync();

        // Publish 2 durable messages
        var pub = CreateClient("pub-ack", "AckPub");
        await pub.ConnectAsync();
        await pub.PublishAsync("test.topic.ack", new TestMessage(1, "a"), durable: true);
        await pub.PublishAsync("test.topic.ack", new TestMessage(2, "b"), durable: true);
        await Task.Delay(200);
        await pub.DisposeAsync();

        // Reconnect subscriber, consume + ack
        var sub2 = CreateClient("sub-ack-2", "AckSub2");
        await sub2.ConnectAsync();
        var firstRound = new List<DurableMessage<TestMessage>>();
        var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var subTask2 = Task.Run(async () =>
        {
            await foreach (var msg in sub2.SubscribeDurableAsync<TestMessage>("test.topic.ack", "ack-sub-id", cts2.Token))
            {
                firstRound.Add(msg);
                await msg.AckAsync(cts2.Token);
                if (firstRound.Count >= 2) break;
            }
        });
        await subTask2;
        await sub2.DisposeAsync();
        firstRound.Should().HaveCount(2);

        // Reconnect again — should receive nothing (queue was acked)
        var sub3 = CreateClient("sub-ack-3", "AckSub3");
        await sub3.ConnectAsync();
        var secondRound = new List<DurableMessage<TestMessage>>();
        var cts3 = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var subTask3 = Task.Run(async () =>
        {
            try
            {
                await foreach (var msg in sub3.SubscribeDurableAsync<TestMessage>("test.topic.ack", "ack-sub-id", cts3.Token))
                    secondRound.Add(msg);
            }
            catch (OperationCanceledException) { }
        });
        try { await subTask3; } catch { }

        secondRound.Should().BeEmpty("acked messages should not be replayed");
        await sub3.DisposeAsync();
    }

    [Test]
    public async Task NonDurablePublish_NotQueuedForDurableSubscribers()
    {
        // Register durable subscriber, disconnect
        var sub1 = CreateClient("sub-nondurable", "NonDurableSub");
        await sub1.ConnectAsync();
        var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var subTask1 = Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in sub1.SubscribeDurableAsync<TestMessage>("test.topic.nondurable", "nondurable-sub-id", cts1.Token)) { }
            }
            catch (OperationCanceledException) { }
        });
        await Task.Delay(300);
        cts1.Cancel();
        try { await subTask1; } catch { }
        await sub1.DisposeAsync();

        // Publish with durable=false
        var pub = CreateClient("pub-nondurable", "NonDurablePub");
        await pub.ConnectAsync();
        await pub.PublishAsync("test.topic.nondurable", new TestMessage(1, "fan-out-only"), durable: false);
        await Task.Delay(200);
        await pub.DisposeAsync();

        // Reconnect — should receive nothing (was not queued)
        var sub2 = CreateClient("sub-nondurable-2", "NonDurableSub2");
        await sub2.ConnectAsync();
        var received = new List<DurableMessage<TestMessage>>();
        var cts2 = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var subTask2 = Task.Run(async () =>
        {
            try
            {
                await foreach (var msg in sub2.SubscribeDurableAsync<TestMessage>("test.topic.nondurable", "nondurable-sub-id", cts2.Token))
                    received.Add(msg);
            }
            catch (OperationCanceledException) { }
        });
        try { await subTask2; } catch { }

        received.Should().BeEmpty("non-durable publishes should not be queued");
        await sub2.DisposeAsync();
    }
}
```

- [ ] **Step 3: Run tests**

Run: `dotnet test src/Tests/Bolt.Tests/Bolt.Tests.csproj --filter "FullyQualifiedName~BoltPubSubIntegrationTests"`
Expected: 5 tests pass.

If a test fails because of timing/race conditions, increase the `Task.Delay(...)` settle delays. The tests use 200-300ms delays which should be enough on a reasonable machine.

- [ ] **Step 4: Commit**

```bash
git add src/Tests/Bolt.Tests/BoltPubSubIntegrationTests.cs
git commit -m "test(bolt): pub/sub integration tests (transient + durable + ack flow)"
```

---

## Task 8: New BoltDriver in XFramework.Integration

**Files:**
- Create: `src/Infrastructure/XFramework.Integration/Drivers/BoltDriver.cs`
- Modify: `src/Infrastructure/XFramework.Integration/XFramework.Integration.csproj`
- Modify: `src/Infrastructure/XFramework.Integration/Abstractions/Wrappers/IMessageBusWrapper.cs`

### Context for implementer

`BoltDriver` is the new `IMessageBusWrapper` implementation backed by `BoltClient`. It coexists with the old `BoltDriverSignalR` until per-service migration is done. The interface gets one new method: `SubscribeDurableAsync`. All existing methods stay backward compatible.

`IMessageBusWrapper` is in `src/Infrastructure/XFramework.Integration/Abstractions/Wrappers/IMessageBusWrapper.cs`. The existing methods take generic types like `TRequest where TRequest : class, IHasRequestServer`. We translate `recipient` (a service name string from `BoltConfiguration.Targets`) to the BoltClient call.

The `XFramework.Integration.csproj` currently references `Microsoft.AspNetCore.SignalR.Client`. We add a project reference to `Bolt.Client.csproj` (or use the NuGet package `Bolt.Net.Client` if preferred — project reference is simpler in a monorepo).

- [ ] **Step 1: Add Bolt.Client project reference**

Open `src/Infrastructure/XFramework.Integration/XFramework.Integration.csproj`. In the `<ItemGroup>` containing `<ProjectReference>` entries, add:

```xml
<ProjectReference Include="..\..\Libraries\Bolt\Bolt.Client\Bolt.Client.csproj" />
```

Do not remove the SignalR.Client reference yet — that happens in Task 12 after all services have migrated.

- [ ] **Step 2: Add SubscribeDurableAsync to IMessageBusWrapper**

Open `src/Infrastructure/XFramework.Integration/Abstractions/Wrappers/IMessageBusWrapper.cs`. Add the new method to the interface (after `Subscribe<TResponse>`):

```csharp
    /// <summary>
    /// Subscribe to a topic with durable delivery semantics. Messages are queued by the Hub
    /// when the subscriber is offline, and replayed on reconnect. The handler must process and
    /// implicitly ack each message (returning normally = ack; throwing = re-delivery on reconnect).
    /// </summary>
    Task SubscribeDurableAsync<TResponse>(string topic, string subscriberId, Func<TResponse, Task> handler, CancellationToken ct = default)
        where TResponse : class;
```

- [ ] **Step 3: Add default no-op SubscribeDurableAsync to BoltDriverSignalR**

Open `src/Infrastructure/XFramework.Integration/Drivers/BoltDriverSignalR.cs`. Add the new method (it's a temporary no-op that throws — `BoltDriverSignalR` doesn't support durable; only `BoltDriver` does):

```csharp
    public Task SubscribeDurableAsync<TResponse>(string topic, string subscriberId, Func<TResponse, Task> handler, CancellationToken ct = default)
        where TResponse : class
    {
        throw new NotSupportedException("Durable subscriptions are only supported by the new BoltDriver. Migrate this service to AddXFrameworkBoltClient.");
    }
```

This keeps the interface satisfied during the migration period.

- [ ] **Step 4: Create BoltDriver.cs**

Create `src/Infrastructure/XFramework.Integration/Drivers/BoltDriver.cs`:

```csharp
using System.Net;
using Bolt.Client;
using MemoryPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Configurations;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Integration.Abstractions.Wrappers;

namespace XFramework.Integration.Drivers;

/// <summary>
/// IMessageBusWrapper backed by the Bolt thin protocol client (Bolt.Client library).
/// Replaces BoltDriverSignalR for services migrated off SignalR.
/// </summary>
public sealed class BoltDriver : IMessageBusWrapper
{
    private readonly BoltClient _client;
    private readonly BoltConfiguration _config;
    private readonly ILogger<BoltDriver> _logger;

    public bool IsConnected => _client.IsConnected;
    public Action OnReconnected { get; set; } = () => { };
    public Action OnReconnecting { get; set; } = () => { };
    public Action OnDisconnected { get; set; } = () => { };

    public BoltDriver(BoltClient client, IOptions<BoltConfiguration> config, ILogger<BoltDriver> logger)
    {
        _client = client;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<bool> Connect()
    {
        try
        {
            await _client.ConnectWithRetryAsync();
            return _client.IsConnected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BoltDriver failed to connect");
            return false;
        }
    }

    public Task StartClientEventListener(string topic) => Task.CompletedTask;

    public async Task<CmdResponse> SendVoidAsync<TRequest>(TRequest request, string recipient)
        where TRequest : class, IHasRequestServer
    {
        EnrichMetadata(request);
        var commandName = typeof(TRequest).Name;
        var (status, _) = await _client.InvokeAsync(recipient, commandName, MemoryPackSerializer.Serialize(request));
        return new CmdResponse { HttpStatusCode = status, Message = status.ToString() };
    }

    public async Task<CmdResponse<TResponse>> SendVoidAsync<TRequest, TResponse>(TRequest request, string recipient)
        where TRequest : class, IHasRequestServer
    {
        EnrichMetadata(request);
        var commandName = typeof(TRequest).Name;
        var (status, payload) = await _client.InvokeAsync(recipient, commandName, MemoryPackSerializer.Serialize(request));
        var response = payload.IsEmpty ? default : MemoryPackSerializer.Deserialize<TResponse>(payload.Span);
        return new CmdResponse<TResponse> { HttpStatusCode = status, Message = status.ToString(), Response = response };
    }

    public async Task<QueryResponse<TResponse>> SendAsync<TRequest, TResponse>(TRequest request, string recipient)
        where TRequest : class, IHasRequestServer
    {
        EnrichMetadata(request);
        var commandName = typeof(TRequest).Name;
        var (status, payload) = await _client.InvokeAsync(recipient, commandName, MemoryPackSerializer.Serialize(request));
        var response = payload.IsEmpty ? default : MemoryPackSerializer.Deserialize<TResponse>(payload.Span);
        return new QueryResponse<TResponse> { HttpStatusCode = status, Message = status.ToString(), Response = response };
    }

    public async Task PublishAsync<TModel>(string eventName, string topic, TModel? data)
        where TModel : class, IHasRequestServer
    {
        if (data is not null) EnrichMetadata(data);
        await _client.PublishAsync(topic, data, durable: false);
    }

    public Task PublishAsync(string eventName, string topic)
        => _client.PublishAsync(topic, new object(), durable: false).AsTask();

    public async Task Subscribe<TResponse>(BoltSubscriptionRequest<TResponse> request)
        where TResponse : class
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in _client.SubscribeAsync<TResponse>(request.Topic))
                    request.Handler?.Invoke(item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transient subscription error: topic={Topic}", request.Topic);
            }
        });
        await Task.CompletedTask;
    }

    public Task Unsubscribe(BoltSubscriptionRequest request)
    {
        // SubscribeAsync's IAsyncEnumerable handles unsubscribe via cancellation token.
        // For now, this is a no-op; a future iteration could expose the CTS.
        return Task.CompletedTask;
    }

    public async Task SubscribeDurableAsync<TResponse>(string topic, string subscriberId, Func<TResponse, Task> handler, CancellationToken ct = default)
        where TResponse : class
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var msg in _client.SubscribeDurableAsync<TResponse>(topic, subscriberId, ct))
                {
                    try
                    {
                        await handler(msg.Payload);
                        await msg.AckAsync(ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Durable handler threw; not acking. topic={Topic} subscriber={Subscriber} seq={Seq}", topic, subscriberId, msg.Sequence);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Durable subscription error: topic={Topic} subscriber={Subscriber}", topic, subscriberId);
            }
        }, ct);
        await Task.CompletedTask;
    }

    private void EnrichMetadata<TRequest>(TRequest request) where TRequest : IHasRequestServer
    {
        request.Metadata ??= new RequestMetadata();
        if (string.IsNullOrEmpty(request.Metadata.ClientName))
            request.Metadata.ClientName = _config.ClientName ?? string.Empty;
        if (request.Metadata.TenantId == Guid.Empty && _config.ClientGuid.HasValue)
            request.Metadata.TenantId = _config.ClientGuid.Value;
    }
}
```

Note: the implementer should adapt the `EnrichMetadata` body to match whatever the existing `BoltDriverSignalR.GetRequestServer<T>` does — copy that logic. The above is a minimal shape.

- [ ] **Step 5: Verify build**

Run: `dotnet build src/Infrastructure/XFramework.Integration/XFramework.Integration.csproj`
Expected: Build succeeds. (`BoltDriverSignalR` is still present; both implementations coexist.)

- [ ] **Step 6: Commit**

```bash
git add src/Infrastructure/XFramework.Integration/Drivers/BoltDriver.cs src/Infrastructure/XFramework.Integration/XFramework.Integration.csproj src/Infrastructure/XFramework.Integration/Abstractions/Wrappers/IMessageBusWrapper.cs src/Infrastructure/XFramework.Integration/Drivers/BoltDriverSignalR.cs
git commit -m "feat(integration): new BoltDriver over BoltClient + SubscribeDurableAsync interface method"
```

---

## Task 9: AddXFrameworkBoltClient DI Extension

**Files:**
- Modify: `src/Infrastructure/XFramework.Integration/Extensions/ServiceCollectionExtensions.cs`

### Context for implementer

This extension method gives services a single call to wire up `BoltClient` + `BoltDriver` + `IMessageBusWrapper`. It reads `BoltConfiguration` from `appsettings.json`, creates a `BoltClient` via the existing `AddBoltClient` builder from `Bolt.Client`, and registers `BoltDriver` as `IMessageBusWrapper`.

- [ ] **Step 1: Add the extension method**

Open `src/Infrastructure/XFramework.Integration/Extensions/ServiceCollectionExtensions.cs` and add:

```csharp
using Bolt.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XFramework.Domain.Shared.Configurations;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;

namespace XFramework.Integration.Extensions;

public static class BoltClientServiceCollectionExtensions
{
    /// <summary>
    /// Register BoltClient (thin protocol) and BoltDriver (IMessageBusWrapper) for service-to-service communication.
    /// Reads BoltConfiguration from appsettings.json (section "BoltConfiguration").
    /// Replaces the legacy SignalR-based driver registration.
    /// </summary>
    public static IServiceCollection AddXFrameworkBoltClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BoltConfiguration>(configuration.GetSection("BoltConfiguration"));

        var boltConfig = configuration.GetSection("BoltConfiguration").Get<BoltConfiguration>()
            ?? throw new InvalidOperationException("BoltConfiguration section is missing or empty in configuration.");

        if (boltConfig.ServerUrls is null || boltConfig.ServerUrls.Count == 0)
            throw new InvalidOperationException("BoltConfiguration:ServerUrls must contain at least one URL.");

        services.AddBoltClient(builder =>
        {
            builder
                .WithServer(boltConfig.ServerUrls[0])
                .WithClientId(boltConfig.ClientGuid?.ToString() ?? Guid.NewGuid().ToString())
                .WithClientName(boltConfig.ClientName ?? "unknown")
                .WithTimeout(boltConfig.RpcTimeoutSeconds);
        });

        services.AddSingleton<IMessageBusWrapper, BoltDriver>();

        return services;
    }
}
```

If the file already exists with other content, add the class as a separate type in the same namespace.

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Infrastructure/XFramework.Integration/XFramework.Integration.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/Infrastructure/XFramework.Integration/Extensions/ServiceCollectionExtensions.cs
git commit -m "feat(integration): AddXFrameworkBoltClient DI extension"
```

---

## Task 10: Migrate IdentityServer to BoltDriver

**Files:**
- Modify: `src/Modules/XFramework.IdentityServer/IdentityServer.Api/Program.cs` (or wherever the SignalR registration lives)
- Modify: `src/Modules/XFramework.IdentityServer/IdentityServer.Api/appsettings.Development.json`
- Modify: `src/Modules/XFramework.IdentityServer/IdentityServer.Api/appsettings.json`
- Modify: `src/Modules/XFramework.IdentityServer/IdentityServer.Api/appsettings.Docker.json`

### Context for implementer

IdentityServer is the canary service. After this task it must function end-to-end (RPC works, can be called by other services). If it works, we proceed to migrate the rest. If not, we debug and fix `BoltDriver` before proceeding.

The current registration is buried in `WrapperInstaller.cs` or similar. Find where `BoltDriverSignalR` (or `IMessageBusWrapper`) is currently registered.

- [ ] **Step 1: Find and update the existing registration**

Search for `BoltDriverSignalR` or `AddSingleton<IMessageBusWrapper` in the IdentityServer.Api project. Replace with:

```csharp
builder.Services.AddXFrameworkBoltClient(builder.Configuration);
```

Remove the old `BoltDriverSignalR` registration line.

- [ ] **Step 2: Update appsettings.Development.json ServerUrls**

Open `src/Modules/XFramework.IdentityServer/IdentityServer.Api/appsettings.Development.json`. In the `BoltConfiguration` section, change:

```json
"ServerUrls": ["http://localhost:7000/stream-flow/queue"],
```

to:

```json
"ServerUrls": ["ws://localhost:7000/bolt/ws"],
```

- [ ] **Step 3: Update appsettings.json ServerUrls (production)**

Open `src/Modules/XFramework.IdentityServer/IdentityServer.Api/appsettings.json`. Find the `BoltConfiguration` section. If `ServerUrls` is missing, add it. Set:

```json
"ServerUrls": ["ws://localhost:7000/bolt/ws"],
```

- [ ] **Step 4: Update appsettings.Docker.json ServerUrls (if file exists)**

If `src/Modules/XFramework.IdentityServer/IdentityServer.Api/appsettings.Docker.json` exists, update its `BoltConfiguration:ServerUrls` to:

```json
"ServerUrls": ["ws://bolt-hub:8080/bolt/ws"],
```

- [ ] **Step 5: Verify IdentityServer builds**

Run: `dotnet build src/Modules/XFramework.IdentityServer/IdentityServer.Api/IdentityServer.Api.csproj`
Expected: Build succeeds.

- [ ] **Step 6: Run IdentityServer integration tests**

Run: `dotnet test src/Tests/IdentityServer.IntegrationTests/IdentityServer.IntegrationTests.csproj`
Expected: All existing tests pass.

If they fail because the test fixture starts a SignalR hub on `/stream-flow/queue` and the client now expects `/bolt/ws`, update the test fixture in `src/Tests/IdentityServer.IntegrationTests/Infrastructure/IntegrationTestFixture.cs` to call `app.UseWebSockets(); app.MapBolt("/bolt/ws");` instead of `app.MapHub<MessageQueueHub>(...)`. The fixture also needs to register `BoltServer` and `IDurableQueueStore` (use `InMemoryDurableQueueStore`).

- [ ] **Step 7: Commit**

```bash
git add src/Modules/XFramework.IdentityServer/ src/Tests/IdentityServer.IntegrationTests/
git commit -m "feat(identity-server): migrate from SignalR to Bolt thin protocol"
```

---

## Task 11: Migrate Remaining Services

### Context for implementer

Repeat Task 10 for each remaining service. For each: replace the old registration with `AddXFrameworkBoltClient(builder.Configuration)`, update `appsettings.*.json` `ServerUrls` to `ws://localhost:7000/bolt/ws` (and `ws://bolt-hub:8080/bolt/ws` for Docker variants), build, run any integration tests if present.

Services to migrate (one commit per service):

1. Wallets — `src/Modules/XFramework.Wallets/Wallets.Api/`
2. Messaging — `src/Modules/XFramework.Messaging/Messaging.Api/`
3. Community — `src/Modules/XFramework.Community/Community.Api/`
4. SmsGateway — `src/Modules/XFramework.SmsGateway/SmsGateway.Api/`
5. Inventario — `src/Modules/XFramework.Inventario/Inventario.Api/`
6. Coins — `src/Modules/XFramework.Coins/Server/Coins.Api/`
7. Gateway — `src/Presentation/Gateway/`
8. ControlPanel.Server — `src/Presentation/ControlPanel.Server/`

For each service, follow these sub-tasks:

- [ ] **Sub-step A: Replace registration call**

In the service's `Program.cs` (or `*Installer.cs` files), find the existing `IMessageBusWrapper` registration (`AddSingleton<IMessageBusWrapper, BoltDriverSignalR>` or similar) and replace with:

```csharp
builder.Services.AddXFrameworkBoltClient(builder.Configuration);
```

- [ ] **Sub-step B: Update all appsettings.json files**

For each `appsettings.json`, `appsettings.Development.json`, `appsettings.Docker.json`, `appsettings.Staging.json` in the service directory, change `BoltConfiguration:ServerUrls` to `ws://localhost:7000/bolt/ws` (or `ws://bolt-hub:8080/bolt/ws` for Docker, or the appropriate environment URL).

- [ ] **Sub-step C: Verify build**

```bash
dotnet build <service-csproj-path>
```

- [ ] **Sub-step D: Run integration tests if present**

```bash
dotnet test <service-tests-csproj-path>
```

- [ ] **Sub-step E: Commit**

```bash
git add <service-paths>
git commit -m "feat(<service-name>): migrate from SignalR to Bolt thin protocol"
```

After all 8 services are committed, no service should reference `BoltDriverSignalR` directly.

---

## Task 12: Remove SignalR from XFramework.Integration

**Files:**
- Delete: `src/Infrastructure/XFramework.Integration/Drivers/BoltDriverSignalR.cs`
- Delete: `src/Infrastructure/XFramework.Integration/Drivers/BaseSignalRHandler.cs`
- Delete: `src/Infrastructure/XFramework.Integration/Services/SignalRService.cs`
- Delete: `src/Infrastructure/XFramework.Integration/Services/ConnectionPool.cs`
- Delete: `src/Infrastructure/XFramework.Integration/Services/PooledRpcCall.cs`
- Delete: `src/Infrastructure/XFramework.Integration/Abstractions/ISignalRService.cs`
- Delete: `src/Infrastructure/XFramework.Integration/Abstractions/ISignalREventHandler.cs`
- Modify: `src/Infrastructure/XFramework.Integration/XFramework.Integration.csproj`

- [ ] **Step 1: Verify no remaining references**

Run: `grep -r "BoltDriverSignalR\|SignalRService\|ISignalRService\|BaseSignalRHandler\|ISignalREventHandler" src/ --include="*.cs"`

Expected: Only references in the files about to be deleted (no other code uses them).

If any code still references these, abort and migrate that code first.

- [ ] **Step 2: Delete the files**

```bash
rm src/Infrastructure/XFramework.Integration/Drivers/BoltDriverSignalR.cs
rm src/Infrastructure/XFramework.Integration/Drivers/BaseSignalRHandler.cs
rm src/Infrastructure/XFramework.Integration/Services/SignalRService.cs
rm src/Infrastructure/XFramework.Integration/Services/ConnectionPool.cs
rm src/Infrastructure/XFramework.Integration/Services/PooledRpcCall.cs
rm src/Infrastructure/XFramework.Integration/Abstractions/ISignalRService.cs
rm src/Infrastructure/XFramework.Integration/Abstractions/ISignalREventHandler.cs
```

- [ ] **Step 3: Remove SignalR.Client package reference**

Open `src/Infrastructure/XFramework.Integration/XFramework.Integration.csproj`. Remove these lines:

```xml
<PackageReference Include="Microsoft.AspNetCore.SignalR.Client" />
<PackageReference Include="Microsoft.AspNetCore.SignalR.Protocols.MessagePack" />
```

(Adjust based on what's actually in the file. Remove all SignalR-related package references.)

- [ ] **Step 4: Verify build**

Run: `dotnet build src/Infrastructure/XFramework.Integration/XFramework.Integration.csproj`
Expected: Build succeeds with 0 errors. If it fails, find and migrate the remaining references.

- [ ] **Step 5: Commit**

```bash
git add src/Infrastructure/XFramework.Integration/
git commit -m "chore(integration): remove SignalR client + drivers (BoltDriverSignalR, SignalRService, etc.)"
```

---

## Task 13: Remove SignalR from Bolt.Hub

**Files:**
- Delete: `src/Modules/XFramework.Bolt/Bolt.Hub/Hubs/MessageQueueHub.cs`
- Modify: `src/Modules/XFramework.Bolt/Bolt.Hub/Installers/BoltInstaller.cs`
- Modify: `src/Modules/XFramework.Bolt/Bolt.Hub/Extensions/ApplicationBuilderExtension.cs`
- Modify: `src/Modules/XFramework.Bolt/Bolt.Hub/Bolt.Hub.csproj`

### Context for implementer

The Hub still has `MessageQueueHub` (the SignalR hub) and registers it via `AddSignalR`. After this task, only `BoltServer` (thin protocol) remains. The `ExecuteQuery`/`ExecuteChanges` methods on `MessageQueueHub` still need to function — they get migrated to Bolt frame handlers in Task 14 below. For this task, we just remove the SignalR plumbing.

- [ ] **Step 1: Delete MessageQueueHub.cs**

```bash
rm src/Modules/XFramework.Bolt/Bolt.Hub/Hubs/MessageQueueHub.cs
```

- [ ] **Step 2: Remove SignalR registration from BoltInstaller**

Open `src/Modules/XFramework.Bolt/Bolt.Hub/Installers/BoltInstaller.cs`. Find and remove the SignalR registration block:

```csharp
// REMOVE THIS:
services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = long.MaxValue;
    options.MaximumParallelInvocationsPerClient = ...;
    options.StreamBufferCapacity = 10;
})
.AddMessagePackProtocol(...);
```

Keep the `BoltServer` and `IDurableQueueStore` registrations from Task 6.

- [ ] **Step 3: Remove MapHub from ApplicationBuilderExtension**

Open `src/Modules/XFramework.Bolt/Bolt.Hub/Extensions/ApplicationBuilderExtension.cs`. Find and remove:

```csharp
// REMOVE THIS:
app.MapHub<MessageQueueHub>("/stream-flow/queue");
```

Keep `app.UseWebSockets();` and `app.MapBolt("/bolt/ws");`.

- [ ] **Step 4: Remove SignalR package references from Bolt.Hub.csproj**

Open `src/Modules/XFramework.Bolt/Bolt.Hub/Bolt.Hub.csproj`. Remove:

```xml
<PackageReference Include="Microsoft.AspNetCore.SignalR" />
<PackageReference Include="Microsoft.AspNetCore.SignalR.Protocols.MessagePack" />
```

(Adjust based on what's actually in the file.)

- [ ] **Step 5: Verify build**

Run: `dotnet build src/Modules/XFramework.Bolt/Bolt.Hub/Bolt.Hub.csproj`
Expected: Build succeeds. If something still references `MessageQueueHub`, find and migrate it.

- [ ] **Step 6: Commit**

```bash
git add src/Modules/XFramework.Bolt/Bolt.Hub/
git commit -m "chore(bolt-hub): remove SignalR — MessageQueueHub, AddSignalR, MapHub"
```

---

## Task 14: ExecuteQuery / ExecuteChanges Bolt Frame Shim

**Files:**
- Modify: `src/Modules/XFramework.Bolt/Bolt.Hub/ThinProtocol/BoltServer.cs`

### Context for implementer

`MessageQueueHub` had `ExecuteQuery`, `StreamQuery`, and `ExecuteChanges` methods that called `IQueryExecutionService` to run LINQ queries against the Hub's local DB. Removing `MessageQueueHub` (Task 13) deletes those methods. Since `RemoteDataContext` clients (Blazor WASM) still call them, we need a Bolt frame-based shim.

The shim is temporary — when the DB proxy decentralization happens (parked work), this shim goes away too. For now, add `ExecuteQuery` (0x0B) and `ExecuteChanges` (0x0C) frame handlers to `BoltServer` that delegate to `IQueryExecutionService`. `StreamQuery` is more complex (returns `IAsyncEnumerable`) — implement it via the existing `BoltStream` mechanism if `RemoteDataContext` uses it; otherwise defer to a follow-up.

For this task, implement only `ExecuteQuery` and `ExecuteChanges` (the synchronous request/response variants). `StreamQuery` is deferred — anyone using it should be flagged as a follow-up.

- [ ] **Step 1: Inject IQueryExecutionService into BoltServer**

Open `src/Modules/XFramework.Bolt/Bolt.Hub/ThinProtocol/BoltServer.cs`. Add `IQueryExecutionService` to the constructor:

```csharp
    private readonly Bolt.Hub.Services.IQueryExecutionService _queryExecutionService;

    public BoltServer(
        ILogger<BoltServer> logger,
        Bolt.Hub.Durable.IDurableQueueStore durableStore,
        Microsoft.Extensions.Options.IOptions<Bolt.Hub.Durable.DurableQueueOptions> durableOptions,
        Bolt.Hub.Services.IQueryExecutionService queryExecutionService)
    {
        _logger = logger;
        _durableStore = durableStore;
        _durableOptions = durableOptions.Value;
        _queryExecutionService = queryExecutionService;
    }
```

- [ ] **Step 2: Add codec methods for ExecuteQuery / ExecuteChanges frames**

These frames carry a request ID and a payload. Open `src/Libraries/Bolt/Bolt.Protocol/Protocol/BoltCodec.cs` and add:

```csharp
    /// <summary>
    /// Encode an ExecuteQuery frame: [1:type=0x0B] [16:requestId] [4:payloadLen] [payload]
    /// </summary>
    public static int WriteExecuteQuery(IBufferWriter<byte> writer, Guid requestId, ReadOnlySpan<byte> payload)
    {
        var totalSize = ExecuteQueryHeaderSize + payload.Length;
        var span = writer.GetSpan(totalSize);

        span[0] = (byte)FrameType.ExecuteQuery;
        WriteGuid(span.Slice(1), requestId);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(17), payload.Length);
        payload.CopyTo(span.Slice(21));

        writer.Advance(totalSize);
        return totalSize;
    }

    /// <summary>
    /// Encode an ExecuteChanges frame: [1:type=0x0C] [16:requestId] [4:payloadLen] [payload]
    /// </summary>
    public static int WriteExecuteChanges(IBufferWriter<byte> writer, Guid requestId, ReadOnlySpan<byte> payload)
    {
        var totalSize = ExecuteChangesHeaderSize + payload.Length;
        var span = writer.GetSpan(totalSize);

        span[0] = (byte)FrameType.ExecuteChanges;
        WriteGuid(span.Slice(1), requestId);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(17), payload.Length);
        payload.CopyTo(span.Slice(21));

        writer.Advance(totalSize);
        return totalSize;
    }

    /// <summary>
    /// Decode an ExecuteQuery or ExecuteChanges frame (same layout for both).
    /// </summary>
    public static bool TryReadDbExecFrame(ReadOnlySpan<byte> buffer, out Guid requestId, out int payloadOffset, out int payloadLength, out int totalSize)
    {
        requestId = Guid.Empty;
        payloadOffset = 0;
        payloadLength = 0;
        totalSize = 0;

        if (buffer.Length < ExecuteQueryHeaderSize) return false;
        var type = (FrameType)buffer[0];
        if (type != FrameType.ExecuteQuery && type != FrameType.ExecuteChanges) return false;

        requestId = ReadGuid(buffer.Slice(1));
        payloadLength = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(17));
        if (payloadLength < 0 || payloadLength > 100 * 1024 * 1024) return false;

        payloadOffset = ExecuteQueryHeaderSize;
        totalSize = ExecuteQueryHeaderSize + payloadLength;
        return buffer.Length >= totalSize;
    }
```

- [ ] **Step 3: Add HandleExecuteQueryFrame and HandleExecuteChangesFrame to BoltServer**

In `BoltServer.cs`:

```csharp
    private async Task HandleExecuteQueryFrameAsync(BoltHubConnection conn, byte[] buffer, int length, CancellationToken ct)
    {
        if (!BoltCodec.TryReadDbExecFrame(buffer.AsSpan(0, length), out var requestId, out var payloadOffset, out var payloadLength, out _))
            return;

        var queryBytes = new byte[payloadLength];
        buffer.AsSpan(payloadOffset, payloadLength).CopyTo(queryBytes);

        try
        {
            var result = await _queryExecutionService.ExecuteAsync(queryBytes, ct);
            var w = Bolt.Protocol.Buffers.RentedBufferWriter.GetThreadLocal();
            BoltCodec.WriteResponse(w, requestId, System.Net.HttpStatusCode.OK, result);
            await conn.SendAsync(w.WrittenMemory, ct);
            w.Reset();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExecuteQuery failed for request {RequestId}", requestId);
            var w = Bolt.Protocol.Buffers.RentedBufferWriter.GetThreadLocal();
            BoltCodec.WriteResponse(w, requestId, System.Net.HttpStatusCode.InternalServerError, ReadOnlySpan<byte>.Empty);
            await conn.SendAsync(w.WrittenMemory, ct);
            w.Reset();
        }
    }

    private async Task HandleExecuteChangesFrameAsync(BoltHubConnection conn, byte[] buffer, int length, CancellationToken ct)
    {
        if (!BoltCodec.TryReadDbExecFrame(buffer.AsSpan(0, length), out var requestId, out var payloadOffset, out var payloadLength, out _))
            return;

        var requestBytes = new byte[payloadLength];
        buffer.AsSpan(payloadOffset, payloadLength).CopyTo(requestBytes);

        try
        {
            var result = await _queryExecutionService.ExecuteChangesAsync(requestBytes, ct);
            var w = Bolt.Protocol.Buffers.RentedBufferWriter.GetThreadLocal();
            BoltCodec.WriteResponse(w, requestId, System.Net.HttpStatusCode.OK, result);
            await conn.SendAsync(w.WrittenMemory, ct);
            w.Reset();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExecuteChanges failed for request {RequestId}", requestId);
            var w = Bolt.Protocol.Buffers.RentedBufferWriter.GetThreadLocal();
            BoltCodec.WriteResponse(w, requestId, System.Net.HttpStatusCode.InternalServerError, ReadOnlySpan<byte>.Empty);
            await conn.SendAsync(w.WrittenMemory, ct);
            w.Reset();
        }
    }
```

- [ ] **Step 4: Wire frame dispatch**

Add cases to the frame dispatch switch in `BoltServer.cs`:

```csharp
            case FrameType.ExecuteQuery:
                await HandleExecuteQueryFrameAsync(connection, buffer, bytesRead, ct);
                break;
            case FrameType.ExecuteChanges:
                await HandleExecuteChangesFrameAsync(connection, buffer, bytesRead, ct);
                break;
```

- [ ] **Step 5: Verify build**

Run: `dotnet build src/Modules/XFramework.Bolt/Bolt.Hub/Bolt.Hub.csproj`
Expected: Build succeeds.

- [ ] **Step 6: Commit**

```bash
git add src/Modules/XFramework.Bolt/Bolt.Hub/ThinProtocol/BoltServer.cs src/Libraries/Bolt/Bolt.Protocol/Protocol/BoltCodec.cs
git commit -m "feat(bolt-hub): ExecuteQuery/ExecuteChanges frame shim (transitional, until DB proxy is decentralized)"
```

Note: After this task, `RemoteDataContext` clients still call `Connection.InvokeAsync("ExecuteQuery", ...)` via SignalR — but that no longer exists. The client side of `RemoteDataContext` needs a separate update to send `ExecuteQuery` Bolt frames instead. That's tracked in the parked DB-proxy decentralization work. For now, document this gap as a follow-up — anyone running Blazor WASM with `RemoteDataContext` will hit it.

---

## Task 15: Final Verification

**Files:** None (verification only)

- [ ] **Step 1: Solution-wide build**

Run: `dotnet build XFramework.slnx -v q`
Expected: 0 errors. Warnings are OK (existing nullability noise).

- [ ] **Step 2: Run all Bolt tests**

Run: `dotnet test src/Tests/Bolt.Tests/Bolt.Tests.csproj -v n`
Expected: All tests pass, including the new pub/sub tests.

- [ ] **Step 3: Run all integration tests**

Run: `dotnet test src/Tests/IdentityServer.IntegrationTests/IdentityServer.IntegrationTests.csproj -v n`
Run: `dotnet test src/Tests/Wallets.IntegrationTests/Wallets.IntegrationTests.csproj -v n`
Expected: All tests pass.

- [ ] **Step 4: Verify no SignalR references in source**

Run: `grep -rn "Microsoft.AspNetCore.SignalR" src/ --include="*.cs" --include="*.csproj"`
Expected: No matches.

Run: `grep -rn "BoltDriverSignalR\|SignalRService\|MessageQueueHub\|BaseSignalRHandler\|ISignalREventHandler" src/ --include="*.cs"`
Expected: No matches.

- [ ] **Step 5: Verify all service appsettings have correct ServerUrls**

Run: `grep -rn "stream-flow/queue\|hubs/v1/messageQueue" src/ --include="*.json"`
Expected: No matches (all migrated to `bolt/ws`).

- [ ] **Step 6: Final commit / push**

If any small fixes were needed during verification, commit them. Push the branch.

```bash
git push -u origin feature/bolt-signalr-removal
```

---

## Self-Review

**Spec coverage:**
- Frame types added: Task 1 ✓
- Codec methods: Task 2 ✓
- Durable queue store interface + in-memory: Task 3 ✓
- Redis durable queue store: Task 4 ✓
- BoltClient pub/sub API (transient + durable + ack + reconnect): Task 5 ✓
- BoltServer pub/sub frame handlers (transient + durable + ack + cleanup): Task 6 ✓
- Pub/sub integration tests: Task 7 ✓
- New BoltDriver implementation: Task 8 ✓
- DI extension method: Task 9 ✓
- IdentityServer migration (canary): Task 10 ✓
- Remaining service migrations: Task 11 ✓
- SignalR removal from XFramework.Integration: Task 12 ✓
- SignalR removal from Bolt.Hub: Task 13 ✓
- ExecuteQuery/ExecuteChanges shim: Task 14 ✓
- Final verification: Task 15 ✓

**Placeholders:** None — every step has actual code or exact commands.

**Type consistency:**
- `BoltCodec.WriteSubscribe` parameters match `TryReadSubscribe` outputs ✓
- `IDurableQueueStore` method signatures consistent across `InMemoryDurableQueueStore` and `RedisDurableQueueStore` ✓
- `DurableMessage<T>` ack callback signature matches `BoltClient.AckAsync` ✓
- `BoltDriver.SubscribeDurableAsync` matches `IMessageBusWrapper.SubscribeDurableAsync` ✓

**Known caveats flagged in plan:**
- Task 5 Step 9: Reconnect re-send placement may need adjustment depending on `BoltClient` internals
- Task 8 Step 4: `EnrichMetadata` should mirror existing `BoltDriverSignalR.GetRequestServer<T>` logic
- Task 14: `RemoteDataContext` client side still calls SignalR-style methods; this is the bridge for the parked DB proxy decentralization
- Task 11: Each service migration is a separate sub-commit; if one service breaks, isolate it
