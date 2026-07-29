using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Threading.Channels;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using Bolt.Server;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public sealed class BoltServerRateLimitingTests
{
    [Test]
    public async Task RequestLimit_IsSharedAcrossPooledConnections_AndIndependentAcrossPrincipals()
    {
        await using var harness = new ServerHarness(new BoltServerOptions
        {
            RpcRequestsPerSecond = 1,
            RpcRequestBurst = 1
        });
        var firstCaller = await harness.ConnectAsync("shared-caller");
        var secondCaller = await harness.ConnectAsync("shared-caller");
        var independentCaller = await harness.ConnectAsync("independent-caller");
        var recipient = await harness.ConnectAsync("request-recipient");

        var firstRequest = Guid.NewGuid();
        firstCaller.Enqueue(WriteRequest(firstRequest, "shared-caller", "request-recipient", "first", [1]));
        await recipient.WaitForFrameAsync(frame => IsRequest(frame, firstRequest));

        var rejectedRequest = Guid.NewGuid();
        secondCaller.Enqueue(WriteRequest(rejectedRequest, "shared-caller", "request-recipient", "second", [2]));
        await secondCaller.WaitForFrameAsync(frame => IsResponse(frame, rejectedRequest, HttpStatusCode.TooManyRequests));

        var independentRequest = Guid.NewGuid();
        independentCaller.Enqueue(WriteRequest(independentRequest, "independent-caller", "request-recipient", "third", [3]));
        await recipient.WaitForFrameAsync(frame => IsRequest(frame, independentRequest));
    }

    [Test]
    public async Task RequestLimit_UsesAuthenticatedPrincipalAcrossDifferentRegisteredClientIds()
    {
        await using var harness = new ServerHarness(new BoltServerOptions
        {
            RpcRequestsPerSecond = 1,
            RpcRequestBurst = 1
        });
        var principal = CreatePrincipal("shared-subject");
        var firstCaller = await harness.ConnectAsync("authenticated-a", principal);
        var secondCaller = await harness.ConnectAsync("authenticated-b", principal);
        var recipient = await harness.ConnectAsync("authenticated-recipient");

        var accepted = Guid.NewGuid();
        firstCaller.Enqueue(WriteRequest(accepted, "authenticated-a", "authenticated-recipient", "first", [1]));
        await recipient.WaitForFrameAsync(frame => IsRequest(frame, accepted));

        var rejected = Guid.NewGuid();
        secondCaller.Enqueue(WriteRequest(rejected, "authenticated-b", "authenticated-recipient", "second", [2]));
        await secondCaller.WaitForFrameAsync(frame => IsResponse(frame, rejected, HttpStatusCode.TooManyRequests));
    }

    [Test]
    public async Task RequestLimit_ReplenishesWithoutReplacingPrincipalState()
    {
        await using var harness = new ServerHarness(new BoltServerOptions
        {
            RpcRequestsPerSecond = 1,
            RpcRequestBurst = 1
        });
        var caller = await harness.ConnectAsync("replenish-caller");
        var recipient = await harness.ConnectAsync("replenish-recipient");

        var acceptedRequest = Guid.NewGuid();
        caller.Enqueue(WriteRequest(acceptedRequest, "replenish-caller", "replenish-recipient", "first", [1]));
        await recipient.WaitForFrameAsync(frame => IsRequest(frame, acceptedRequest));

        var rejectedRequest = Guid.NewGuid();
        caller.Enqueue(WriteRequest(rejectedRequest, "replenish-caller", "replenish-recipient", "second", [2]));
        await caller.WaitForFrameAsync(frame => IsResponse(frame, rejectedRequest, HttpStatusCode.TooManyRequests));

        await Task.Delay(TimeSpan.FromMilliseconds(1_100));

        var replenishedRequest = Guid.NewGuid();
        caller.Enqueue(WriteRequest(replenishedRequest, "replenish-caller", "replenish-recipient", "third", [3]));
        await recipient.WaitForFrameAsync(frame => IsRequest(frame, replenishedRequest));
        harness.Server.GetHealthSnapshot().ActiveRateLimitPrincipals.Should().Be(2);
    }

    [Test]
    public async Task RequestLimit_NonDivisibleRate_ReplenishesAtConfiguredRate()
    {
        await using var harness = new ServerHarness(new BoltServerOptions
        {
            RpcRequestsPerSecond = 11,
            RpcRequestBurst = 1
        });
        var caller = await harness.ConnectAsync("exact-rate-caller");
        var recipient = await harness.ConnectAsync("exact-rate-recipient");

        var first = Guid.NewGuid();
        caller.Enqueue(WriteRequest(first, "exact-rate-caller", "exact-rate-recipient", "first", [1]));
        await recipient.WaitForFrameAsync(frame => IsRequest(frame, first));

        await Task.Delay(TimeSpan.FromMilliseconds(120));

        var second = Guid.NewGuid();
        caller.Enqueue(WriteRequest(second, "exact-rate-caller", "exact-rate-recipient", "second", [2]));
        await recipient.WaitForFrameAsync(frame => IsRequest(frame, second));
    }

    [Test]
    public async Task PrincipalLimiter_IsRetainedUntilLastPooledConnectionDisconnects_ThenRemoved()
    {
        await using var harness = new ServerHarness(new BoltServerOptions
        {
            RpcRequestsPerSecond = 10,
            RpcRequestBurst = 10
        });
        var first = await harness.ConnectAsync("cleanup-caller");
        var second = await harness.ConnectAsync("cleanup-caller");

        harness.Server.GetHealthSnapshot().ActiveRateLimitPrincipals.Should().Be(1);

        first.Complete();
        await harness.WaitUntilAsync(() =>
        {
            var snapshot = harness.Server.GetHealthSnapshot();
            return snapshot.RegisteredConnections == 1 && snapshot.ActiveRateLimitPrincipals == 1;
        });

        second.Complete();
        await harness.WaitUntilAsync(() => harness.Server.GetHealthSnapshot().ActiveRateLimitPrincipals == 0);
    }

    [Test]
    public async Task ByteLimit_RejectsBeforeRoutingPayload()
    {
        await using var harness = new ServerHarness(new BoltServerOptions
        {
            RpcInboundBytesPerSecond = 4,
            RpcInboundByteBurst = 4
        });
        var caller = await harness.ConnectAsync("byte-caller");
        var barrier = await harness.ConnectAsync("byte-barrier");
        var recipient = await harness.ConnectAsync("byte-recipient");

        var rejectedRequest = Guid.NewGuid();
        caller.Enqueue(WriteRequest(rejectedRequest, "byte-caller", "byte-recipient", "too-large", new byte[5]));
        await caller.WaitForFrameAsync(frame => IsResponse(frame, rejectedRequest, HttpStatusCode.TooManyRequests));

        var barrierRequest = Guid.NewGuid();
        barrier.Enqueue(WriteRequest(barrierRequest, "byte-barrier", "byte-recipient", "barrier", [1]));
        await recipient.WaitForFrameAsync(frame => IsRequest(frame, barrierRequest));
        recipient.GetLogicalFrames().Should().NotContain(frame => IsRequest(frame, rejectedRequest));
    }

    [Test]
    public async Task SpoofedRequest_ConsumesPrincipalAllowanceBeforeRejection()
    {
        await using var harness = new ServerHarness(new BoltServerOptions
        {
            RpcRequestsPerSecond = 1,
            RpcRequestBurst = 1
        });
        var caller = await harness.ConnectAsync("spoof-caller");
        var recipient = await harness.ConnectAsync("spoof-recipient");

        caller.Enqueue(WriteRequest(Guid.NewGuid(), "forged-sender", "spoof-recipient", "forged", [1]));

        var rejected = Guid.NewGuid();
        caller.Enqueue(WriteRequest(rejected, "spoof-caller", "spoof-recipient", "valid", [2]));
        await caller.WaitForFrameAsync(frame => IsResponse(frame, rejected, HttpStatusCode.TooManyRequests));
        recipient.GetLogicalFrames().Should().NotContain(frame => IsRequest(frame, rejected));
    }

    [Test]
    public async Task MalformedRequest_ConsumesPrincipalAllowanceBeforeParseRejection()
    {
        await using var harness = new ServerHarness(new BoltServerOptions
        {
            RpcRequestsPerSecond = 1,
            RpcRequestBurst = 1
        });
        var caller = await harness.ConnectAsync("malformed-caller");
        var recipient = await harness.ConnectAsync("malformed-recipient");

        caller.Enqueue([(byte)FrameType.Request]);

        var rejected = Guid.NewGuid();
        caller.Enqueue(WriteRequest(rejected, "malformed-caller", "malformed-recipient", "valid", [1]));
        await caller.WaitForFrameAsync(frame => IsResponse(frame, rejected, HttpStatusCode.TooManyRequests));
        recipient.GetLogicalFrames().Should().NotContain(frame => IsRequest(frame, rejected));
    }

    [Test]
    public async Task TruncatedStreamData_ConsumesPrincipalAllowanceBeforeRouteParsing()
    {
        await using var harness = new ServerHarness(new BoltServerOptions
        {
            RpcRequestsPerSecond = 1,
            RpcRequestBurst = 1
        });
        var caller = await harness.ConnectAsync("truncated-stream-caller");
        var recipient = await harness.ConnectAsync("truncated-stream-recipient");

        caller.Enqueue([(byte)FrameType.StreamData]);

        var rejected = Guid.NewGuid();
        caller.Enqueue(WriteRequest(rejected, "truncated-stream-caller", "truncated-stream-recipient", "valid", [1]));
        await caller.WaitForFrameAsync(frame => IsResponse(frame, rejected, HttpStatusCode.TooManyRequests));
        recipient.GetLogicalFrames().Should().NotContain(frame => IsRequest(frame, rejected));
    }

    [Test]
    public async Task LargeRpc_IsChargedOnceAtLogicalAdmission_AndRejectedWith429()
    {
        await using var harness = new ServerHarness(new BoltServerOptions
        {
            RpcRequestsPerSecond = 1,
            RpcRequestBurst = 1
        });
        var caller = await harness.ConnectAsync("large-caller");
        var recipient = await harness.ConnectAsync("large-recipient");

        var streamId = Guid.NewGuid();
        caller.Enqueue(WriteFrame(writer => BoltCodec.WriteStreamOpen(
            writer,
            streamId,
            BoltCodec.Fnv1aHash("large-recipient"),
            BoltCodec.Fnv1aHash("__bolt_large_rpc__"))));
        await recipient.WaitForFrameAsync(frame => IsStreamFrame(frame, FrameType.StreamOpen, streamId));

        var largeRequestId = Guid.NewGuid();
        var header = new byte[28];
        largeRequestId.TryWriteBytes(header);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16), BoltCodec.Fnv1aHash("large-command"));
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(20), 1);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(24), BoltCodec.Fnv1aHash("large-caller"));
        caller.Enqueue(WriteFrame(writer => BoltCodec.WriteStreamData(writer, streamId, header)));
        await recipient.WaitForFrameAsync(frame => IsStreamFrame(frame, FrameType.StreamData, streamId));

        var beforeBodyCount = recipient.GetLogicalFrames().Count;
        caller.Enqueue(WriteFrame(writer => BoltCodec.WriteStreamData(writer, streamId, [42])));
        await recipient.WaitForLogicalFrameCountAsync(beforeBodyCount + 1);
        recipient.GetLogicalFrames().Should().Contain(frame =>
            IsStreamDataWithPayload(frame, streamId, new byte[] { 42 }));

        var rejectedRequest = Guid.NewGuid();
        caller.Enqueue(WriteRequest(rejectedRequest, "large-caller", "large-recipient", "after-large", [1]));
        await caller.WaitForFrameAsync(frame => IsResponse(frame, rejectedRequest, HttpStatusCode.TooManyRequests));
    }

    [Test]
    public async Task LargeRpc_ByteAdmissionFailure_ClosesStreamWith429()
    {
        await using var harness = new ServerHarness(new BoltServerOptions
        {
            RpcInboundBytesPerSecond = 4,
            RpcInboundByteBurst = 4
        });
        var caller = await harness.ConnectAsync("large-byte-caller");
        var recipient = await harness.ConnectAsync("large-byte-recipient");
        var streamId = Guid.NewGuid();

        caller.Enqueue(WriteFrame(writer => BoltCodec.WriteStreamOpen(
            writer,
            streamId,
            BoltCodec.Fnv1aHash("large-byte-recipient"),
            BoltCodec.Fnv1aHash("__bolt_large_rpc__"))));
        await recipient.WaitForFrameAsync(frame => IsStreamFrame(frame, FrameType.StreamOpen, streamId));

        var header = new byte[28];
        Guid.NewGuid().TryWriteBytes(header);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16), BoltCodec.Fnv1aHash("large-command"));
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(20), 5);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(24), BoltCodec.Fnv1aHash("large-byte-caller"));
        caller.Enqueue(WriteFrame(writer => BoltCodec.WriteStreamData(writer, streamId, header)));

        await caller.WaitForFrameAsync(frame =>
            BoltCodec.TryReadStreamClose(frame, out var actualStreamId, out var status) &&
            actualStreamId == streamId &&
            status == HttpStatusCode.TooManyRequests);
    }

    [Test]
    public async Task SpoofedLargeRpcMetadata_ConsumesPrincipalAllowanceBeforeRejection()
    {
        await using var harness = new ServerHarness(new BoltServerOptions
        {
            RpcRequestsPerSecond = 1,
            RpcRequestBurst = 1
        });
        var caller = await harness.ConnectAsync("large-spoof-caller");
        var recipient = await harness.ConnectAsync("large-spoof-recipient");
        var streamId = Guid.NewGuid();

        caller.Enqueue(WriteFrame(writer => BoltCodec.WriteStreamOpen(
            writer,
            streamId,
            BoltCodec.Fnv1aHash("large-spoof-recipient"),
            BoltCodec.Fnv1aHash("__bolt_large_rpc__"))));
        await recipient.WaitForFrameAsync(frame => IsStreamFrame(frame, FrameType.StreamOpen, streamId));

        var header = new byte[28];
        Guid.NewGuid().TryWriteBytes(header);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16), BoltCodec.Fnv1aHash("large-command"));
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(20), 1);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(24), BoltCodec.Fnv1aHash("forged-large-sender"));
        caller.Enqueue(WriteFrame(writer => BoltCodec.WriteStreamData(writer, streamId, header)));

        await caller.WaitForFrameAsync(frame =>
            BoltCodec.TryReadStreamClose(frame, out var actualStreamId, out var status) &&
            actualStreamId == streamId &&
            status == HttpStatusCode.Forbidden);

        var rejected = Guid.NewGuid();
        caller.Enqueue(WriteRequest(rejected, "large-spoof-caller", "large-spoof-recipient", "valid", [1]));
        await caller.WaitForFrameAsync(frame => IsResponse(frame, rejected, HttpStatusCode.TooManyRequests));
    }

    [Test]
    public async Task MalformedKnownLargeRpcStreamData_ConsumesPrincipalAllowance()
    {
        await using var harness = new ServerHarness(new BoltServerOptions
        {
            RpcRequestsPerSecond = 1,
            RpcRequestBurst = 1
        });
        var caller = await harness.ConnectAsync("malformed-large-caller");
        var recipient = await harness.ConnectAsync("malformed-large-recipient");
        var streamId = Guid.NewGuid();

        caller.Enqueue(WriteFrame(writer => BoltCodec.WriteStreamOpen(
            writer,
            streamId,
            BoltCodec.Fnv1aHash("malformed-large-recipient"),
            BoltCodec.Fnv1aHash("__bolt_large_rpc__"))));
        await recipient.WaitForFrameAsync(frame => IsStreamFrame(frame, FrameType.StreamOpen, streamId));

        var malformed = new byte[17];
        malformed[0] = (byte)FrameType.StreamData;
        streamId.TryWriteBytes(malformed.AsSpan(1));
        caller.Enqueue(malformed);
        await caller.WaitForFrameAsync(frame =>
            BoltCodec.TryReadStreamClose(frame, out var actualStreamId, out var status) &&
            actualStreamId == streamId &&
            status == HttpStatusCode.BadRequest);

        var rejected = Guid.NewGuid();
        caller.Enqueue(WriteRequest(rejected, "malformed-large-caller", "malformed-large-recipient", "valid", [1]));
        await caller.WaitForFrameAsync(frame => IsResponse(frame, rejected, HttpStatusCode.TooManyRequests));
    }

    [Test]
    public async Task PushLimit_DropsRejectedPush_AndRecordsMetric()
    {
        await using var harness = new ServerHarness(new BoltServerOptions
        {
            RpcRequestsPerSecond = 1,
            RpcRequestBurst = 1
        });
        var sender = await harness.ConnectAsync("push-sender");
        var barrier = await harness.ConnectAsync("push-barrier");
        var recipient = await harness.ConnectAsync("push-recipient");
        var before = harness.Server.GetHealthSnapshot().PushRateLimitRejections;
        var acceptedCommand = BoltCodec.Fnv1aHash("accepted-push");
        var rejectedCommand = BoltCodec.Fnv1aHash("rejected-push");
        var barrierCommand = BoltCodec.Fnv1aHash("barrier-push");

        sender.Enqueue(WritePush("push-sender", "push-recipient", acceptedCommand, [1]));
        await recipient.WaitForFrameAsync(frame => IsPush(frame, acceptedCommand));
        sender.Enqueue(WritePush("push-sender", "push-recipient", rejectedCommand, [2]));
        barrier.Enqueue(WritePush("push-barrier", "push-recipient", barrierCommand, [3]));
        await recipient.WaitForFrameAsync(frame => IsPush(frame, barrierCommand));

        recipient.GetLogicalFrames().Should().NotContain(frame => IsPush(frame, rejectedCommand));
        harness.Server.GetHealthSnapshot().PushRateLimitRejections.Should().BeGreaterThan(before);
    }

    [Test]
    public async Task InvalidLargeRpcResponsePush_IsNotExemptFromPrincipalLimit()
    {
        await using var harness = new ServerHarness(new BoltServerOptions
        {
            RpcRequestsPerSecond = 1,
            RpcRequestBurst = 1
        });
        var sender = await harness.ConnectAsync("reserved-push-sender");
        var recipient = await harness.ConnectAsync("reserved-push-recipient");
        var before = harness.Server.GetHealthSnapshot().PushRateLimitRejections;

        sender.Enqueue(WritePush(
            "reserved-push-sender",
            "reserved-push-recipient",
            BoltCodec.Fnv1aHash("ordinary"),
            [1]));
        await recipient.WaitForFrameAsync(frame => IsPush(frame, BoltCodec.Fnv1aHash("ordinary")));

        sender.Enqueue(WritePush(
            "reserved-push-sender",
            "reserved-push-recipient",
            BoltCodec.Fnv1aHash("__bolt_large_rpc_response__"),
            new byte[18]));

        await harness.WaitUntilAsync(() =>
            harness.Server.GetHealthSnapshot().PushRateLimitRejections > before);
    }

    [Test]
    public async Task SpoofedPush_ConsumesPrincipalAllowanceBeforeRejection()
    {
        await using var harness = new ServerHarness(new BoltServerOptions
        {
            RpcRequestsPerSecond = 1,
            RpcRequestBurst = 1
        });
        var sender = await harness.ConnectAsync("spoof-push-sender");
        var recipient = await harness.ConnectAsync("spoof-push-recipient");
        var before = harness.Server.GetHealthSnapshot().PushRateLimitRejections;

        sender.Enqueue(WritePush("forged-push-sender", "spoof-push-recipient", 101, [1]));
        sender.Enqueue(WritePush("spoof-push-sender", "spoof-push-recipient", 102, [2]));

        await harness.WaitUntilAsync(() =>
            harness.Server.GetHealthSnapshot().PushRateLimitRejections > before);
        recipient.GetLogicalFrames().Should().NotContain(frame => IsPush(frame, 101) || IsPush(frame, 102));
    }

    [Test]
    public async Task MalformedPush_ConsumesPrincipalAllowanceBeforeParseRejection()
    {
        await using var harness = new ServerHarness(new BoltServerOptions
        {
            RpcRequestsPerSecond = 1,
            RpcRequestBurst = 1
        });
        var sender = await harness.ConnectAsync("malformed-push-sender");
        var recipient = await harness.ConnectAsync("malformed-push-recipient");
        var before = harness.Server.GetHealthSnapshot().PushRateLimitRejections;

        sender.Enqueue([(byte)FrameType.Push]);
        sender.Enqueue(WritePush("malformed-push-sender", "malformed-push-recipient", 103, [1]));

        await harness.WaitUntilAsync(() =>
            harness.Server.GetHealthSnapshot().PushRateLimitRejections > before);
        recipient.GetLogicalFrames().Should().NotContain(frame => IsPush(frame, 103));
    }

    [Test]
    public async Task HealthRateLimitTotals_AreScopedToServerInstance()
    {
        var options = new BoltServerOptions { RpcRequestsPerSecond = 1, RpcRequestBurst = 1 };
        await using var firstHarness = new ServerHarness(options);
        await using var secondHarness = new ServerHarness(options);
        var caller = await firstHarness.ConnectAsync("instance-caller");
        var recipient = await firstHarness.ConnectAsync("instance-recipient");

        var accepted = Guid.NewGuid();
        caller.Enqueue(WriteRequest(accepted, "instance-caller", "instance-recipient", "first", [1]));
        await recipient.WaitForFrameAsync(frame => IsRequest(frame, accepted));
        var rejected = Guid.NewGuid();
        caller.Enqueue(WriteRequest(rejected, "instance-caller", "instance-recipient", "second", [2]));
        await caller.WaitForFrameAsync(frame => IsResponse(frame, rejected, HttpStatusCode.TooManyRequests));

        firstHarness.Server.GetHealthSnapshot().RequestRateLimitRejections.Should().Be(1);
        secondHarness.Server.GetHealthSnapshot().RequestRateLimitRejections.Should().Be(0);
    }

    [Test]
    public async Task ZeroRecipientPush_IsRouteMiss_NotBroadcast()
    {
        await using var harness = new ServerHarness(new BoltServerOptions());
        var sender = await harness.ConnectAsync("zero-sender");
        var firstRecipient = await harness.ConnectAsync("zero-first");
        var secondRecipient = await harness.ConnectAsync("zero-second");
        var zeroCommand = BoltCodec.Fnv1aHash("zero-recipient");
        var validCommand = BoltCodec.Fnv1aHash("valid-recipient");

        sender.Enqueue(WriteFrame(writer => BoltCodec.WritePush(
            writer,
            Guid.NewGuid(),
            0,
            BoltCodec.Fnv1aHash("zero-sender"),
            zeroCommand,
            [1])));
        sender.Enqueue(WritePush("zero-sender", "zero-first", validCommand, [2]));
        await firstRecipient.WaitForFrameAsync(frame => IsPush(frame, validCommand));

        firstRecipient.GetLogicalFrames().Should().NotContain(frame => IsPush(frame, zeroCommand));
        secondRecipient.GetLogicalFrames().Should().NotContain(frame => IsPush(frame, zeroCommand));
    }

    [Test]
    public void TopicAuthorization_DefaultIsOptional_ButRequiredModeFailsWithoutAuthorizer()
    {
        using var optional = new BoltServer(NullLogger<BoltServer>.Instance, new BoltServerOptions());

        var act = () => new BoltServer(
            NullLogger<BoltServer>.Instance,
            new BoltServerOptions { RequireTopicAuthorization = true });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*IBoltTopicAuthorizer*");
    }

    [Test]
    public void AddBoltServer_RequiredTopicAuthorization_FailsWhenServerIsResolvedWithoutAuthorizer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBoltServer(options => options.RequireTopicAuthorization = true);
        using var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<BoltServer>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*IBoltTopicAuthorizer*");
    }

    private static byte[] WriteRequest(
        Guid requestId,
        string sender,
        string recipient,
        string command,
        byte[] payload) =>
        WriteFrame(writer => BoltCodec.WriteRequest(
            writer,
            requestId,
            BoltCodec.Fnv1aHash(recipient),
            BoltCodec.Fnv1aHash(sender),
            BoltCodec.Fnv1aHash(command),
            payload));

    private static ClaimsPrincipal CreatePrincipal(string subject) =>
        new(new ClaimsIdentity([new Claim("sub", subject)], "test"));

    private static byte[] WritePush(string sender, string recipient, int commandHash, byte[] payload) =>
        WriteFrame(writer => BoltCodec.WritePush(
            writer,
            Guid.NewGuid(),
            BoltCodec.Fnv1aHash(recipient),
            BoltCodec.Fnv1aHash(sender),
            commandHash,
            payload));

    private static byte[] WriteFrame(Action<IBufferWriter<byte>> write)
    {
        var writer = new ArrayBufferWriter<byte>();
        write(writer);
        return writer.WrittenSpan.ToArray();
    }

    private static bool IsRequest(byte[] frame, Guid requestId) =>
        BoltCodec.TryReadRequest(frame, out var request, out _) && request.RequestId == requestId;

    private static bool IsResponse(byte[] frame, Guid requestId, HttpStatusCode status) =>
        BoltCodec.TryReadResponse(frame, out var response, out _) &&
        response.RequestId == requestId &&
        response.StatusCode == status;

    private static bool IsPush(byte[] frame, int commandHash) =>
        frame.Length > 0 && frame[0] == (byte)FrameType.Push &&
        BoltCodec.TryReadRequest(frame, out var push, out _) &&
        push.CommandHash == commandHash;

    private static bool IsStreamFrame(byte[] frame, FrameType frameType, Guid streamId) =>
        frame.Length >= 17 && frame[0] == (byte)frameType && BoltCodec.ReadStreamId(frame) == streamId;

    private static bool IsStreamDataWithPayload(byte[] frame, Guid streamId, byte[] expected) =>
        BoltCodec.TryReadStreamData(frame, out var actualStreamId, out var payloadOffset, out var payloadLength, out _) &&
        actualStreamId == streamId &&
        frame.AsSpan(payloadOffset, payloadLength).SequenceEqual(expected);

    private sealed class ServerHarness : IAsyncDisposable
    {
        private readonly List<TestConnection> _connections = [];
        private readonly List<Task> _connectionTasks = [];

        public ServerHarness(BoltServerOptions options) =>
            Server = new BoltServer(NullLogger<BoltServer>.Instance, options);

        public BoltServer Server { get; }

        public async Task<TestConnection> ConnectAsync(
            string clientId,
            ClaimsPrincipal? principal = null)
        {
            var connection = new TestConnection();
            _connections.Add(connection);
            _connectionTasks.Add(Server.HandleConnectionAsync(connection, principal, CancellationToken.None));
            connection.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, clientId, clientId)));
            await connection.WaitForFrameAsync(frame => frame.Length > 0 && frame[0] == (byte)FrameType.RegisterAck);
            connection.SentFrames.Clear();
            return connection;
        }

        public async Task WaitUntilAsync(Func<bool> predicate)
        {
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < deadline)
            {
                if (predicate())
                    return;
                await Task.Delay(10);
            }

            throw new TimeoutException("Expected Bolt server state was not observed.");
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var connection in _connections)
                connection.Complete();
            await Task.WhenAll(_connectionTasks).WaitAsync(TimeSpan.FromSeconds(3));
            Server.Dispose();
            foreach (var connection in _connections)
                await connection.DisposeAsync();
        }
    }

    private sealed class TestConnection : IBoltConnection
    {
        private readonly Channel<byte[]> _incoming = Channel.CreateUnbounded<byte[]>();
        private int _connected = 1;

        public ConcurrentQueue<byte[]> SentFrames { get; } = new();
        public bool SupportsDatagrams => false;
        public bool IsConnected => Volatile.Read(ref _connected) != 0;
        public BoltTransport TransportType => BoltTransport.WebSocket;

        public void Enqueue(byte[] frame) => _incoming.Writer.TryWrite(frame).Should().BeTrue();

        public void Complete()
        {
            Interlocked.Exchange(ref _connected, 0);
            _incoming.Writer.TryComplete();
        }

        public List<byte[]> GetLogicalFrames()
        {
            var frames = new List<byte[]>();
            foreach (var message in SentFrames)
            {
                if (!BoltCodec.TryReadBatch(message, out var batch))
                {
                    frames.Add(message);
                    continue;
                }

                foreach (var frame in batch)
                    frames.Add(frame.ToArray());
            }

            return frames;
        }

        public async Task WaitForFrameAsync(Func<byte[], bool> predicate)
        {
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < deadline)
            {
                if (GetLogicalFrames().Any(predicate))
                    return;
                await Task.Delay(10);
            }

            throw new TimeoutException("Expected Bolt frame was not observed.");
        }

        public async Task WaitForLogicalFrameCountAsync(int count)
        {
            await WaitForFrameAsync(_ => GetLogicalFrames().Count >= count);
        }

        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            SentFrames.Enqueue(data.ToArray());
            return ValueTask.CompletedTask;
        }

        public async ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(
            Memory<byte> buffer,
            CancellationToken ct = default)
        {
            try
            {
                var frame = await _incoming.Reader.ReadAsync(ct);
                frame.CopyTo(buffer);
                return (frame.Length, true);
            }
            catch (ChannelClosedException)
            {
                return (0, true);
            }
        }

        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
            ValueTask.CompletedTask;

        public ValueTask CloseAsync(CancellationToken ct = default)
        {
            Complete();
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Complete();
            return ValueTask.CompletedTask;
        }
    }
}
