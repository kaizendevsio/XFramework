using System.Net.WebSockets;
using Bolt.Protocol.Transport;
using FluentAssertions;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public class WebSocketBoltConnectionTests
{
    [Test]
    public void TransportType_IsWebSocket()
    {
        using var ws = new ClientWebSocket();
        var conn = new WebSocketBoltConnection(ws);
        conn.TransportType.Should().Be(BoltTransport.WebSocket);
    }

    [Test]
    public void SupportsDatagrams_IsFalse()
    {
        using var ws = new ClientWebSocket();
        var conn = new WebSocketBoltConnection(ws);
        conn.SupportsDatagrams.Should().BeFalse();
    }

    [Test]
    public async Task SendDatagramAsync_IsNoOp()
    {
        using var ws = new ClientWebSocket();
        var conn = new WebSocketBoltConnection(ws);
        await conn.SendDatagramAsync(new byte[] { 1, 2, 3 });
    }
}

[TestFixture]
public class QuicFramingTests
{
    [Test]
    public void WriteLengthPrefix_ReadsBackCorrectly()
    {
        var payload = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var framed = new byte[4 + payload.Length];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(framed, (uint)payload.Length);
        payload.CopyTo(framed.AsSpan(4));

        var readLen = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(framed);
        readLen.Should().Be(5);
        framed.AsSpan(4, (int)readLen).ToArray().Should().Equal(payload);
    }

    [Test]
    public void WriteLengthPrefix_LargePayload_CorrectLength()
    {
        var payload = new byte[1_048_576];
        Random.Shared.NextBytes(payload);
        var framed = new byte[4 + payload.Length];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(framed, (uint)payload.Length);
        payload.CopyTo(framed.AsSpan(4));

        var readLen = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(framed);
        readLen.Should().Be(1_048_576);
    }
}
