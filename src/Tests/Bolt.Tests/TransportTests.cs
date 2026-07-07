using System.Net.WebSockets;
using Bolt.Client;
using Bolt.Client.Transport;
using Bolt.Protocol.Transport;
using Bolt.Server;
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
public class WebTransportBoltConnectionTests
{
    [Test]
    public async Task ReceiveAsync_FragmentedPrefixAndBody_ReturnsCompleteMessage()
    {
        var body = new byte[] { 1, 2, 3, 4, 5, 6 };
        var stream = new FragmentedReadStream(LengthPrefixed(body), maxReadSize: 1);
        var connection = new WebTransportBoltConnection(stream);
        var buffer = new byte[16];

        var (bytesRead, endOfMessage) = await connection.ReceiveAsync(buffer);

        bytesRead.Should().Be(body.Length);
        endOfMessage.Should().BeTrue();
        buffer.AsSpan(0, bytesRead).ToArray().Should().Equal(body);
    }

    [Test]
    public async Task ReceiveAsync_EofDuringPrefix_ReturnsClosedWithoutData()
    {
        var stream = new FragmentedReadStream(new byte[] { 6, 0 }, maxReadSize: 1);
        var connection = new WebTransportBoltConnection(stream);
        var buffer = new byte[16];

        var (bytesRead, endOfMessage) = await connection.ReceiveAsync(buffer);

        bytesRead.Should().Be(0);
        endOfMessage.Should().BeTrue();
        connection.IsConnected.Should().BeFalse();
    }

    [Test]
    public async Task ReceiveAsync_EofDuringBody_ReturnsClosedWithoutPartialData()
    {
        var data = LengthPrefixed(new byte[] { 1, 2 });
        data[0] = 4;
        var stream = new FragmentedReadStream(data, maxReadSize: 1);
        var connection = new WebTransportBoltConnection(stream);
        var buffer = new byte[16];

        var (bytesRead, endOfMessage) = await connection.ReceiveAsync(buffer);

        bytesRead.Should().Be(0);
        endOfMessage.Should().BeTrue();
        connection.IsConnected.Should().BeFalse();
    }

    [Test]
    public async Task ReceiveAsync_ZeroLengthMessage_ReturnsClosedWithoutSpin()
    {
        var stream = new FragmentedReadStream(new byte[] { 0, 0, 0, 0 }, maxReadSize: 4);
        var connection = new WebTransportBoltConnection(stream);
        var buffer = new byte[16];

        var (bytesRead, endOfMessage) = await connection.ReceiveAsync(buffer);

        bytesRead.Should().Be(0);
        endOfMessage.Should().BeTrue();
        connection.IsConnected.Should().BeFalse();
    }

    [Test]
    public async Task ReceiveAsync_MessageLargerThanBuffer_ReturnsExactChunks()
    {
        var body = new byte[] { 1, 2, 3, 4, 5 };
        var stream = new FragmentedReadStream(LengthPrefixed(body), maxReadSize: 1);
        var connection = new WebTransportBoltConnection(stream);
        var buffer = new byte[2];

        var first = await connection.ReceiveAsync(buffer);
        first.BytesRead.Should().Be(2);
        first.EndOfMessage.Should().BeFalse();
        buffer.ToArray().Should().Equal(1, 2);

        var second = await connection.ReceiveAsync(buffer);
        second.BytesRead.Should().Be(2);
        second.EndOfMessage.Should().BeFalse();
        buffer.ToArray().Should().Equal(3, 4);

        var third = await connection.ReceiveAsync(buffer);
        third.BytesRead.Should().Be(1);
        third.EndOfMessage.Should().BeTrue();
        buffer[0].Should().Be(5);
    }

    private static byte[] LengthPrefixed(byte[] body)
    {
        var data = new byte[4 + body.Length];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0, 4), (uint)body.Length);
        body.CopyTo(data.AsSpan(4));
        return data;
    }

    private sealed class FragmentedReadStream(byte[] data, int maxReadSize) : Stream
    {
        private int _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => data.Length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_position >= data.Length)
                return ValueTask.FromResult(0);

            var bytesToRead = Math.Min(Math.Min(maxReadSize, buffer.Length), data.Length - _position);
            data.AsMemory(_position, bytesToRead).CopyTo(buffer);
            _position += bytesToRead;
            return ValueTask.FromResult(bytesToRead);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= data.Length)
                return 0;

            var bytesToRead = Math.Min(Math.Min(maxReadSize, count), data.Length - _position);
            Array.Copy(data, _position, buffer, offset, bytesToRead);
            _position += bytesToRead;
            return bytesToRead;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

[TestFixture]
public class WebSocketTransportConnectTests
{
    private WebApplication _hubApp = null!;
    private const int WsPort = 18700;

    [OneTimeSetUp]
    public async Task Setup()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{WsPort}");
        builder.Services.AddBoltServer();
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        _hubApp = builder.Build();
        _hubApp.UseWebSockets();
        _hubApp.MapBolt("/bolt");
        _hubApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _hubApp.RunAsync());
        await WaitForHealth($"http://localhost:{WsPort}/health");
    }

    [Test]
    public async Task WebSocket_ConnectsSuccessfully()
    {
        var lf = _hubApp.Services.GetRequiredService<ILoggerFactory>();
        var opts = new BoltClientOptions
        {
            RpcTimeoutSeconds = 10,
            TransportAttemptTimeoutMs = 3000,
            PreferredTransports = [BoltTransport.WebSocket]
        };

        var client = new BoltClient(
            new Uri($"ws://localhost:{WsPort}/bolt"),
            "ws_connect_client", "WsConnectClient", opts, lf.CreateLogger<BoltClient>());
        await client.ConnectAsync();

        client.IsConnected.Should().BeTrue();

        await client.DisposeAsync();
    }

    [OneTimeTearDown]
    public async Task Cleanup()
    {
        try { if (_hubApp != null) await _hubApp.StopAsync(); } catch { }
    }

    private static async Task WaitForHealth(string url, int timeoutSeconds = 15)
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if ((await client.GetAsync(url)).IsSuccessStatusCode) return;
            }
            catch { }
            await Task.Delay(100);
        }
        throw new TimeoutException($"Service at {url} not healthy within {timeoutSeconds}s");
    }
}
