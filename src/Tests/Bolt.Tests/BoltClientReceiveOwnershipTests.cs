using System.Reflection;
using System.Runtime.InteropServices;
using Bolt.Client;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public sealed class BoltClientReceiveOwnershipTests
{
    [Test]
    public async Task ReceiveLoop_CustomFrame_UsesCallbackScopedReceiveBuffer()
    {
        var frame = new byte[] { (byte)FrameType.MediaFrame, 10, 20, 30 };
        var transport = new SingleFrameBoltConnection(frame);
        var connection = new BoltConnection(transport);
        await using var client = new BoltClient(
            new Uri("ws://localhost:1/bolt"),
            "frame_client",
            "FrameClient",
            new BoltClientOptions(),
            NullLogger<BoltClient>.Instance);
        byte[]? callbackBuffer = null;
        byte[]? callbackCopy = null;
        client.RegisterFrameHandler(FrameType.MediaFrame, (_, buffer, length) =>
        {
            callbackBuffer = buffer;
            callbackCopy = buffer.AsSpan(0, length).ToArray();
        });

        var receiveLoop = typeof(BoltClient).GetMethod(
            "ReceiveLoopAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        await ((Task)receiveLoop.Invoke(client, [connection, CancellationToken.None])!);

        callbackCopy.Should().Equal(frame);
        callbackBuffer.Should().BeSameAs(transport.ReceiveBuffer);
    }

    private sealed class SingleFrameBoltConnection(byte[] frame) : IBoltConnection
    {
        private bool _delivered;
        public byte[]? ReceiveBuffer { get; private set; }
        public bool SupportsDatagrams => false;
        public bool IsConnected { get; private set; } = true;
        public BoltTransport TransportType => BoltTransport.WebSocket;

        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => ValueTask.CompletedTask;

        public ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(
            Memory<byte> buffer,
            CancellationToken ct = default)
        {
            if (_delivered)
            {
                IsConnected = false;
                return ValueTask.FromResult((0, true));
            }

            MemoryMarshal.TryGetArray<byte>((ReadOnlyMemory<byte>)buffer, out var segment).Should().BeTrue();
            ReceiveBuffer = segment.Array;
            frame.AsMemory().CopyTo(buffer);
            _delivered = true;
            return ValueTask.FromResult((frame.Length, true));
        }

        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask CloseAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
