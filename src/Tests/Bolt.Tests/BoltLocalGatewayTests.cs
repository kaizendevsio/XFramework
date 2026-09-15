using System.Buffers;
using System.Net;
using System.Security.Claims;
using System.Threading.Channels;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using Bolt.Server;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture, CancelAfter(15000)]
public sealed class BoltLocalGatewayTests
{
    [TestCase(false, "issued", "issued")]
    [TestCase(true, "issued", "another")]
    [TestCase(true, null, "issued")]
    public async Task Registration_RejectsUnauthenticatedMissingOrMismatchedBinding(bool authenticated, string? claim, string client)
    {
        using var server = Server();
        await using var transport = new Transport();
        var run = server.HandleConnectionAsync(transport, User(authenticated, claim), CancellationToken.None, true);
        transport.Input.Writer.TryWrite(Register(client));
        var response = await transport.Output.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.That(BoltCodec.TryReadRegisterAck(response, out var accepted, out _), Is.True);
        Assert.That(accepted, Is.False);
        await run.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [TestCase(FrameType.Push)]
    [TestCase(FrameType.Subscribe)]
    [TestCase(FrameType.Publish)]
    [TestCase(FrameType.Response)]
    [TestCase(FrameType.StreamOpen)]
    [TestCase(FrameType.MediaFrame)]
    public async Task PublicMode_ClosesUnsupportedFrames(FrameType type)
    {
        using var server = Server();
        await using var transport = new Transport();
        var run = server.HandleConnectionAsync(transport, User(true, "issued"), CancellationToken.None, true);
        transport.Input.Writer.TryWrite(Register("issued"));
        await transport.Output.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        transport.Input.Writer.TryWrite([(byte)type]);
        await run.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.That(transport.IsConnected, Is.False);
    }

    [Test]
    public async Task LocalRpcAndHostPush_WorkButUnknownRpcCannotRouteToAnotherClient()
    {
        using var server = Server();
        await using var first = new Transport();
        await using var second = new Transport();
        using var lifetime = new CancellationTokenSource();
        var registered = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.ClientRegistered += (value, _) => { if (value.ClientId == "issued") registered.TrySetResult(value.ConnectionId); return Task.CompletedTask; };
        server.RegisterHandler("chat", (BoltRequestContext context, ReadOnlyMemory<byte> payload, Guid _, CancellationToken _) =>
        {
            Assert.That(context.User!.FindFirst("binding")!.Value, Is.EqualTo("issued"));
            return Task.FromResult((HttpStatusCode.OK, payload));
        });
        var run1 = server.HandleConnectionAsync(first, User(true, "issued"), lifetime.Token, true);
        var run2 = server.HandleConnectionAsync(second, User(true, "peer"), lifetime.Token, true);
        first.Input.Writer.TryWrite(Register("issued"));
        second.Input.Writer.TryWrite(Register("peer"));
        await first.Output.Reader.ReadAsync();
        await second.Output.Reader.ReadAsync();

        var requestId = Guid.NewGuid();
        first.Input.Writer.TryWrite(Request(requestId, "chat", "peer"));
        var response = await first.Output.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.That(BoltCodec.TryReadResponse(response, out var frame, out _), Is.True);
        Assert.That(frame.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        first.Input.Writer.TryWrite(Request(Guid.NewGuid(), "not-supported", "peer"));
        response = await first.Output.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.That(BoltCodec.TryReadResponse(response, out frame, out _), Is.True);
        Assert.That(frame.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(second.Output.Reader.TryRead(out _), Is.False);

        await server.SendPushAsync(await registered.Task, "event", new byte[] { 42 });
        response = await first.Output.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.That(response[0], Is.EqualTo((byte)FrameType.Push));
        Assert.That(BoltCodec.TryReadRequest(response, out var push, out _), Is.True);
        Assert.That(push.RecipientHash, Is.EqualTo(BoltCodec.Fnv1aHash("issued")));
        Assert.That(push.GetPayload(response).ToArray(), Is.EqualTo(new byte[] { 42 }));
        Assert.That(second.Output.Reader.TryRead(out _), Is.False);
        lifetime.Cancel();
        await Task.WhenAll(run1, run2).WaitAsync(TimeSpan.FromSeconds(3));
    }

    private static BoltServer Server() => new(NullLogger<BoltServer>.Instance, new BoltServerOptions
        { LocalHandlersOnly = true, RequireSecureTransport = true, RegistrationClientIdClaim = "binding" });
    private static ClaimsPrincipal User(bool authenticated, string? binding) => new(new ClaimsIdentity(
        binding is null ? [new Claim(ClaimTypes.NameIdentifier, "account")] : [new Claim(ClaimTypes.NameIdentifier, "account"), new Claim("binding", binding)],
        authenticated ? "test" : null));
    private static byte[] Register(string client)
    { var writer = new ArrayBufferWriter<byte>(); BoltCodec.WriteRegister(writer, client, "browser"); return writer.WrittenSpan.ToArray(); }
    private static byte[] Request(Guid id, string command, string recipient)
    {
        var writer = new ArrayBufferWriter<byte>();
        BoltCodec.WriteRequest(writer, id, BoltCodec.Fnv1aHash(recipient), BoltCodec.Fnv1aHash("issued"), BoltCodec.Fnv1aHash(command), new byte[] { 1 });
        return writer.WrittenSpan.ToArray();
    }
    private sealed class Transport : IBoltConnection
    {
        public Channel<byte[]> Input { get; } = Channel.CreateUnbounded<byte[]>();
        public Channel<byte[]> Output { get; } = Channel.CreateUnbounded<byte[]>();
        public bool IsConnected { get; private set; } = true;
        public bool SupportsDatagrams => false;
        public BoltTransport TransportType => BoltTransport.WebSocket;
        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => Output.Writer.WriteAsync(data.ToArray(), ct);
        public async ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            try { var bytes = await Input.Reader.ReadAsync(ct); bytes.CopyTo(buffer); return (bytes.Length, true); }
            catch (ChannelClosedException) { return (0, true); }
        }
        public ValueTask CloseAsync(CancellationToken ct = default) { IsConnected = false; Input.Writer.TryComplete(); return ValueTask.CompletedTask; }
        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => CloseAsync();
    }
}
