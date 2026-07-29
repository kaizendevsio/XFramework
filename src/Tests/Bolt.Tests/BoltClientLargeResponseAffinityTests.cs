using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using Bolt.Client;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public sealed class BoltClientLargeResponseAffinityTests
{
    [Test]
    public async Task LargeRpcResponse_OpenDataAndClose_UseOneSelectedConnection()
    {
        var firstTransport = new RecordingBoltConnection();
        var secondTransport = new RecordingBoltConnection();
        var firstConnection = new BoltConnection(firstTransport);
        var secondConnection = new BoltConnection(secondTransport);
        firstConnection.StartSendLoop(CancellationToken.None);
        secondConnection.StartSendLoop(CancellationToken.None);
        await using var client = new BoltClient(
            new Uri("ws://localhost:1/bolt"),
            "affinity_client",
            "AffinityClient",
            new BoltClientOptions
            {
                LargePayloadThreshold = 16,
                StreamChunkSize = 16
            },
            NullLogger<BoltClient>.Instance);
        // Put the inbound connection second so a fresh pool selection would choose the wrong one.
        Attach(client, secondConnection, firstConnection);
        client.RegisterHandler("large_response", (_, _) => Task.FromResult(
            (HttpStatusCode.OK, (ReadOnlyMemory<byte>)new byte[64])));
        typeof(BoltClient)
            .GetMethod("RegisterLargeRpcStreamHandler", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(client, null);

        var inbound = CreateStream(firstConnection);
        var requestId = Guid.NewGuid();
        var header = new byte[28];
        requestId.TryWriteBytes(header);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16), BoltCodec.Fnv1aHash("large_response"));
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(20), 1);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(24), 42);
        var payload = new byte[] { 7 };
        Enqueue(inbound, header).Should().BeTrue();
        Enqueue(inbound, payload).Should().BeTrue();
        MarkClosed(inbound);

        var handlers = (ConcurrentDictionary<int, Func<BoltStream, Task>>)typeof(BoltClient)
            .GetField("_streamHandlers", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(client)!;
        await handlers[BoltCodec.Fnv1aHash("__bolt_large_rpc__")](inbound);

        firstTransport.Frames.Should().NotBeEmpty();
        secondTransport.Frames.Should().BeEmpty();
        firstTransport.Frames.Select(frame => BoltCodec.PeekFrameType(frame)).Should().ContainInOrder(
            FrameType.StreamOpen,
            FrameType.StreamData,
            FrameType.StreamClose);
    }

    private static void Attach(BoltClient client, params BoltConnection[] connections)
    {
        var list = (List<BoltConnection>)typeof(BoltClient)
            .GetField("_connections", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(client)!;
        list.AddRange(connections);
        typeof(BoltClient)
            .GetField("_isRegistered", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(client, true);
    }

    private static BoltStream CreateStream(BoltConnection connection)
    {
        var constructor = typeof(BoltStream).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(Guid), typeof(BoltConnection), typeof(Action<Guid>), typeof(int)],
            modifiers: null)!;
        return (BoltStream)constructor.Invoke([Guid.NewGuid(), connection, null!, 8]);
    }

    private static bool Enqueue(BoltStream stream, ReadOnlyMemory<byte> data) =>
        (bool)typeof(BoltStream)
            .GetMethod("TryEnqueueInbound", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(stream, [data])!;

    private static void MarkClosed(BoltStream stream) =>
        typeof(BoltStream)
            .GetMethod("MarkClosed", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(stream, [HttpStatusCode.OK]);

    private sealed class RecordingBoltConnection : IBoltConnection
    {
        public List<byte[]> Frames { get; } = [];
        public bool SupportsDatagrams => false;
        public bool IsConnected { get; private set; } = true;
        public BoltTransport TransportType => BoltTransport.WebSocket;

        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            Frames.Add(data.ToArray());
            return ValueTask.CompletedTask;
        }

        public ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(
            Memory<byte> buffer,
            CancellationToken ct = default) => ValueTask.FromResult((0, true));
        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask CloseAsync(CancellationToken ct = default)
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
