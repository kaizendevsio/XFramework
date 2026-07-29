using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Security.Claims;
using System.Net;
using System.Reflection;
using System.Threading.Channels;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using Bolt.Server;
using Bolt.Server.Durable;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using XFramework.Domain.Shared.ServiceIdentity;

namespace Bolt.Tests;

[TestFixture]
public sealed class BoltPhase0ContainmentTests
{
    [Test]
    public async Task TopicAuthorizerException_DeniesFrameWithoutStoppingReceiveLoop()
    {
        var authorizer = new ThrowingTopicAuthorizer();
        using var server = new BoltServer(
            NullLogger<BoltServer>.Instance,
            new BoltServerOptions(),
            [authorizer]);

        await using var transport = new ChannelBoltConnection();
        var serverTask = server.HandleConnectionAsync(transport, CancellationToken.None);
        transport.Enqueue(WriteFrame(writer =>
            BoltCodec.WriteRegister(writer, "ordinary-client", "OrdinaryClient")));
        await transport.WaitForSentFramesAsync(1);
        transport.Enqueue(WriteFrame(writer =>
            BoltCodec.WriteSubscribe(writer, "test.topic", "subscriber-1", durable: false)));
        transport.Enqueue(WriteFrame(writer =>
            BoltCodec.WriteSubscribe(writer, "test.topic", "subscriber-2", durable: false)));
        await authorizer.WaitForCallsAsync(2);
        transport.Complete();
        await serverTask.WaitAsync(TimeSpan.FromSeconds(3));

        authorizer.CallCount.Should().Be(2);
        transport.SentFrames.Should().ContainSingle();
        transport.SentFrames.TryPeek(out var registerAck).Should().BeTrue();
        AssertRegisterAck(registerAck, expectedSuccess: true);
    }

    [Test]
    public async Task MediaDisabled_RejectsCallSignalWithoutCreatingResponseTraffic()
    {
        using var server = new BoltServer(
            NullLogger<BoltServer>.Instance,
            new BoltServerOptions { MediaEnabled = false });
        var initiatePayload = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(initiatePayload, 12345);

        await using var transport = new ChannelBoltConnection();
        var serverTask = server.HandleConnectionAsync(transport, CancellationToken.None);
        transport.Enqueue(WriteFrame(writer =>
            BoltCodec.WriteRegister(writer, "ordinary-client", "OrdinaryClient")));
        await transport.WaitForSentFramesAsync(1);
        transport.Enqueue(WriteFrame(writer => BoltCodec.WriteCallSignal(
            writer,
            Guid.NewGuid(),
            SignalType.Initiate,
            initiatePayload)));
        transport.Complete();
        await serverTask.WaitAsync(TimeSpan.FromSeconds(3));

        transport.SentFrames.Should().ContainSingle();
        transport.SentFrames.TryPeek(out var registerAck).Should().BeTrue();
        AssertRegisterAck(registerAck, expectedSuccess: true);
    }

    [Test]
    public async Task AuthenticatedConnection_ClosesAtTokenExpiration()
    {
        using var server = new BoltServer(
            NullLogger<BoltServer>.Instance,
            new BoltServerOptions { MaxConnectionLifetimeSeconds = 30 });
        await using var transport = new BlockingBoltConnection();
        var identity = new ClaimsIdentity(
            [new Claim("exp", DateTimeOffset.UtcNow.AddSeconds(1).ToUnixTimeSeconds().ToString())],
            "test");

        await server.HandleConnectionAsync(
                transport,
                new ClaimsPrincipal(identity),
                CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(3));

        transport.CloseCalled.Should().BeTrue();
    }

    [Test]
    public async Task AuthenticatedConnection_NonResponsiveCloseIsAbortedWithinConfiguredDeadline()
    {
        using var server = new BoltServer(
            NullLogger<BoltServer>.Instance,
            new BoltServerOptions
            {
                MaxConnectionLifetimeSeconds = 1,
                TransportCloseTimeoutMs = 50
            });
        await using var transport = new NonResponsiveCloseBoltConnection();
        var identity = new ClaimsIdentity(
            [new Claim("exp", DateTimeOffset.UtcNow.AddSeconds(1).ToUnixTimeSeconds().ToString())],
            "test");

        await server.HandleConnectionAsync(
                transport,
                new ClaimsPrincipal(identity),
                CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(3));

        transport.CloseCalled.Should().BeTrue();
        transport.DisposeCalled.Should().BeTrue();
    }

    [Test]
    public async Task AuthenticatedConnection_NonResponsiveDisconnectSubscriberCannotDelayClose()
    {
        using var server = new BoltServer(
            NullLogger<BoltServer>.Instance,
            new BoltServerOptions
            {
                MaxConnectionLifetimeSeconds = 1,
                TransportCloseTimeoutMs = 50
            });
        server.ClientDisconnected += (_, _) => Task.Delay(Timeout.InfiniteTimeSpan);
        await using var transport = new ChannelBoltConnection();
        var identity = new ClaimsIdentity(
            [
                new Claim("sub", "disconnect-timeout-user"),
                new Claim("exp", DateTimeOffset.UtcNow.AddSeconds(1).ToUnixTimeSeconds().ToString())
            ],
            "test");
        var serverTask = server.HandleConnectionAsync(
            transport,
            new ClaimsPrincipal(identity),
            CancellationToken.None);

        transport.Enqueue(WriteFrame(writer =>
            BoltCodec.WriteRegister(writer, "disconnect-timeout-client", "BrowserClient")));
        await transport.WaitForSentFramesAsync(1);

        await serverTask.WaitAsync(TimeSpan.FromSeconds(3));

        transport.IsConnected.Should().BeFalse();
    }

    [Test]
    public async Task Dispose_CancelsActiveConnectionLoops()
    {
        var server = new BoltServer(NullLogger<BoltServer>.Instance, new BoltServerOptions());
        await using var transport = new BlockingBoltConnection();
        var serverTask = server.HandleConnectionAsync(transport, CancellationToken.None);

        server.Dispose();

        await serverTask.WaitAsync(TimeSpan.FromSeconds(3));
        transport.CloseCalled.Should().BeTrue();
    }

    [Test]
    public async Task ClientConnectionLimit_RejectsAdditionalConnectionForSameIdentity()
    {
        using var server = new BoltServer(
            NullLogger<BoltServer>.Instance,
            new BoltServerOptions { MaxConnectionsPerPrincipal = 1 });
        await using var first = new ChannelBoltConnection();
        await using var second = new ChannelBoltConnection();
        var firstTask = server.HandleConnectionAsync(first, CancellationToken.None);
        var secondTask = server.HandleConnectionAsync(second, CancellationToken.None);

        first.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "limited-client", "LimitedClient")));
        await first.WaitForSentFramesAsync(1);
        second.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "limited-client", "LimitedClient")));
        await second.WaitForSentFramesAsync(1);

        AssertRegisterAck(second.SentFrames.Single(), expectedSuccess: false);
        first.Complete();
        second.Complete();
        await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Test]
    public async Task ClientConnectionLimit_UsesAuthenticatedPrincipalAcrossRotatingClientIds()
    {
        using var server = new BoltServer(
            NullLogger<BoltServer>.Instance,
            new BoltServerOptions { MaxConnectionsPerPrincipal = 1 });
        await using var first = new ChannelBoltConnection();
        await using var second = new ChannelBoltConnection();
        var principal = CreateUserPrincipal("shared-user");
        var firstTask = server.HandleConnectionAsync(first, principal, CancellationToken.None);
        var secondTask = server.HandleConnectionAsync(second, principal, CancellationToken.None);

        first.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "rotating-a", "BrowserA")));
        await first.WaitForSentFramesAsync(1);
        second.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "rotating-b", "BrowserB")));
        await second.WaitForSentFramesAsync(1);

        AssertRegisterAck(second.SentFrames.Single(), expectedSuccess: false);
        var serviceRouteField = typeof(BoltServer).GetField(
            "_connectionsByServiceHash",
            BindingFlags.Instance | BindingFlags.NonPublic);
        serviceRouteField.Should().NotBeNull();
        var registeredRoutes = serviceRouteField!.GetValue(server)!;
        var values = (System.Collections.IEnumerable)registeredRoutes
            .GetType()
            .GetProperty("Values")!
            .GetValue(registeredRoutes)!;
        var route = values.Cast<object>().Should().ContainSingle().Subject;
        route.GetType().GetProperty("ClientId")!.GetValue(route).Should().Be("rotating-a");

        first.Complete();
        second.Complete();
        await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Test]
    public async Task RegistrationMigrationAllowance_RequiresExactUnexpiredMapping()
    {
        var options = new BoltServerOptions
        {
            RegistrationIdentityBindingMode = BoltRegistrationIdentityBindingMode.Enforce
        };
        options.RegistrationMigrationAllowances.Add(new BoltRegistrationMigrationAllowance
        {
            AuthenticatedServiceName = "XFramework.Current",
            ClientId = "legacy-id",
            ClientName = "XFramework.Legacy",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5)
        });
        using var server = new BoltServer(NullLogger<BoltServer>.Instance, options);
        await using var transport = new ChannelBoltConnection();
        var serverTask = server.HandleConnectionAsync(
            transport,
            CreateServicePrincipal("XFramework.Current"),
            CancellationToken.None);

        transport.Enqueue(WriteFrame(writer =>
            BoltCodec.WriteRegister(writer, "legacy-id", "XFramework.Legacy")));
        await transport.WaitForSentFramesAsync(1);

        AssertRegisterAck(transport.SentFrames.Single(), expectedSuccess: true);
        transport.Complete();
        await serverTask.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Test]
    public async Task ReservedServicePools_RejectUserPrincipalForEveryKnownServiceName()
    {
        var options = new BoltServerOptions
        {
            RegistrationIdentityBindingMode = BoltRegistrationIdentityBindingMode.Enforce
        };
        options.ReservedServiceNames.AddRange(XFrameworkServiceNames.All);
        using var server = new BoltServer(NullLogger<BoltServer>.Instance, options);

        foreach (var serviceName in XFrameworkServiceNames.All)
        {
            await using var transport = new ScriptedBoltConnection(
                WriteFrame(writer => BoltCodec.WriteRegister(
                    writer,
                    Sha256Hex(serviceName),
                    serviceName)));

            await server.HandleConnectionAsync(
                transport,
                CreateUserPrincipal("ordinary-user"),
                CancellationToken.None);

            AssertRegisterAck(transport.SentFrames.Single(), expectedSuccess: false);
        }
    }

    [Test]
    public void RegistrationMigrationAllowance_ExpiredEntryFailsServerStartup()
    {
        var options = new BoltServerOptions();
        options.RegistrationMigrationAllowances.Add(new BoltRegistrationMigrationAllowance
        {
            AuthenticatedServiceName = "XFramework.Current",
            ClientId = "legacy-id",
            ClientName = "XFramework.Legacy",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1)
        });

        var act = () => new BoltServer(NullLogger<BoltServer>.Instance, options);

        act.Should().Throw<InvalidOperationException>().WithMessage("*expired*");
    }

    [Test]
    public void RegistrationMigrationAllowance_ExcessiveLifetimeFailsServerStartup()
    {
        var options = new BoltServerOptions();
        options.RegistrationMigrationAllowances.Add(new BoltRegistrationMigrationAllowance
        {
            AuthenticatedServiceName = "XFramework.Current",
            ClientId = "legacy-id",
            ClientName = "XFramework.Legacy",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(8)
        });

        var act = () => new BoltServer(NullLogger<BoltServer>.Instance, options);

        act.Should().Throw<InvalidOperationException>().WithMessage("*seven-day maximum*");
    }

    [Test]
    public async Task SecurityRejection_EmitsBoundedMetric()
    {
        long disabledMediaRejections = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "Bolt.Server" &&
                instrument.Name == "bolt.server.media.disabled_rejections")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) =>
            Interlocked.Add(ref disabledMediaRejections, measurement));
        listener.Start();

        using var server = new BoltServer(
            NullLogger<BoltServer>.Instance,
            new BoltServerOptions { MediaEnabled = false });
        await using var transport = new ScriptedBoltConnection(
            WriteFrame(writer => BoltCodec.WriteRegister(writer, "ordinary-client", "OrdinaryClient")),
            WriteFrame(writer => BoltCodec.WriteCallSignal(
                writer,
                Guid.NewGuid(),
                SignalType.End,
                ReadOnlySpan<byte>.Empty)));

        await server.HandleConnectionAsync(transport, CancellationToken.None);

        disabledMediaRejections.Should().Be(1);
    }

    [Test]
    public async Task PendingRpcLimit_ReturnsTooManyRequestsWithoutForwardingExcessCall()
    {
        using var server = new BoltServer(
            NullLogger<BoltServer>.Instance,
            new BoltServerOptions { MaxPendingRpcCalls = 1 });
        await using var caller = new ChannelBoltConnection();
        await using var recipient = new ChannelBoltConnection();
        var callerTask = server.HandleConnectionAsync(caller, CancellationToken.None);
        var recipientTask = server.HandleConnectionAsync(recipient, CancellationToken.None);

        caller.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "caller", "Caller")));
        recipient.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "recipient", "Recipient")));
        await Task.WhenAll(caller.WaitForSentFramesAsync(1), recipient.WaitForSentFramesAsync(1));

        var recipientHash = BoltCodec.Fnv1aHash("recipient");
        var callerHash = BoltCodec.Fnv1aHash("caller");
        caller.Enqueue(WriteFrame(writer => BoltCodec.WriteRequest(
            writer,
            Guid.NewGuid(),
            recipientHash,
            callerHash,
            BoltCodec.Fnv1aHash("first"),
            ReadOnlySpan<byte>.Empty)));
        await recipient.WaitForSentFramesAsync(2);

        caller.Enqueue(WriteFrame(writer => BoltCodec.WriteRequest(
            writer,
            Guid.NewGuid(),
            recipientHash,
            callerHash,
            BoltCodec.Fnv1aHash("second"),
            ReadOnlySpan<byte>.Empty)));
        await caller.WaitForSentFramesAsync(2);

        var responseBytes = caller.SentFrames.ToArray()[1];
        BoltCodec.TryReadResponse(responseBytes, out var response, out _).Should().BeTrue();
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        recipient.SentFrames.Should().HaveCount(2);

        caller.Complete();
        recipient.Complete();
        await Task.WhenAll(callerTask, recipientTask).WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Test]
    public async Task PendingRpcPerPrincipalLimit_IsolatesCapacityBetweenPrincipals()
    {
        using var server = new BoltServer(
            NullLogger<BoltServer>.Instance,
            new BoltServerOptions
            {
                MaxPendingRpcCalls = 2,
                MaxPendingRpcCallsPerPrincipal = 1
            });
        await using var callerA = new ChannelBoltConnection();
        await using var callerB = new ChannelBoltConnection();
        await using var recipient = new ChannelBoltConnection();
        var callerATask = server.HandleConnectionAsync(
            callerA,
            CreateUserPrincipal("pending-principal-a"),
            CancellationToken.None);
        var callerBTask = server.HandleConnectionAsync(
            callerB,
            CreateUserPrincipal("pending-principal-b"),
            CancellationToken.None);
        var recipientTask = server.HandleConnectionAsync(
            recipient,
            CreateUserPrincipal("pending-recipient"),
            CancellationToken.None);

        callerA.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "pending-a", "PendingA")));
        callerB.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "pending-b", "PendingB")));
        recipient.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "pending-target", "PendingTarget")));
        await Task.WhenAll(
            callerA.WaitForSentFramesAsync(1),
            callerB.WaitForSentFramesAsync(1),
            recipient.WaitForSentFramesAsync(1));

        var recipientHash = BoltCodec.Fnv1aHash("pending-target");
        callerA.Enqueue(WriteFrame(writer => BoltCodec.WriteRequest(
            writer,
            Guid.NewGuid(),
            recipientHash,
            BoltCodec.Fnv1aHash("pending-a"),
            BoltCodec.Fnv1aHash("first-a"),
            ReadOnlySpan<byte>.Empty)));
        await recipient.WaitForSentFramesAsync(2);

        callerA.Enqueue(WriteFrame(writer => BoltCodec.WriteRequest(
            writer,
            Guid.NewGuid(),
            recipientHash,
            BoltCodec.Fnv1aHash("pending-a"),
            BoltCodec.Fnv1aHash("second-a"),
            ReadOnlySpan<byte>.Empty)));
        await callerA.WaitForSentFramesAsync(2);

        var rejectedBytes = callerA.SentFrames.ToArray()[1];
        BoltCodec.TryReadResponse(rejectedBytes, out var rejected, out _).Should().BeTrue();
        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        callerB.Enqueue(WriteFrame(writer => BoltCodec.WriteRequest(
            writer,
            Guid.NewGuid(),
            recipientHash,
            BoltCodec.Fnv1aHash("pending-b"),
            BoltCodec.Fnv1aHash("first-b"),
            ReadOnlySpan<byte>.Empty)));
        await recipient.WaitForSentFramesAsync(3);
        recipient.SentFrames.Should().HaveCount(3);

        callerA.Complete();
        callerB.Complete();
        recipient.Complete();
        await Task.WhenAll(callerATask, callerBTask, recipientTask).WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Test]
    public async Task ActiveStreamLimit_RejectsExcessAndReleasesSlotOnClose()
    {
        using var server = new BoltServer(
            NullLogger<BoltServer>.Instance,
            new BoltServerOptions { MaxActiveStreamsPerPrincipal = 1 });
        await using var sender = new ChannelBoltConnection();
        await using var recipient = new ChannelBoltConnection();
        var senderTask = server.HandleConnectionAsync(sender, CancellationToken.None);
        var recipientTask = server.HandleConnectionAsync(recipient, CancellationToken.None);

        sender.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "stream-sender", "StreamSender")));
        recipient.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "stream-recipient", "StreamRecipient")));
        await Task.WhenAll(sender.WaitForSentFramesAsync(1), recipient.WaitForSentFramesAsync(1));

        var recipientHash = BoltCodec.Fnv1aHash("stream-recipient");
        var firstStreamId = Guid.NewGuid();
        sender.Enqueue(WriteFrame(writer => BoltCodec.WriteStreamOpen(
            writer,
            firstStreamId,
            recipientHash,
            BoltCodec.Fnv1aHash("stream"))));
        await recipient.WaitForSentFramesAsync(2);

        sender.Enqueue(WriteFrame(writer => BoltCodec.WriteStreamOpen(
            writer,
            Guid.NewGuid(),
            recipientHash,
            BoltCodec.Fnv1aHash("stream"))));
        await Task.Delay(50);
        recipient.SentFrames.Should().HaveCount(2);

        sender.Enqueue(WriteFrame(writer => BoltCodec.WriteStreamClose(writer, firstStreamId)));
        await recipient.WaitForSentFramesAsync(3);
        sender.Enqueue(WriteFrame(writer => BoltCodec.WriteStreamOpen(
            writer,
            Guid.NewGuid(),
            recipientHash,
            BoltCodec.Fnv1aHash("stream"))));
        await recipient.WaitForSentFramesAsync(4);

        sender.Complete();
        recipient.Complete();
        await Task.WhenAll(senderTask, recipientTask).WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Test]
    public async Task ActiveStreamLimit_AggregatesAcrossConnectionsForAuthenticatedPrincipal()
    {
        using var server = new BoltServer(
            NullLogger<BoltServer>.Instance,
            new BoltServerOptions { MaxActiveStreamsPerPrincipal = 1 });
        await using var firstSender = new ChannelBoltConnection();
        await using var secondSender = new ChannelBoltConnection();
        await using var recipient = new ChannelBoltConnection();
        var sharedPrincipal = CreateUserPrincipal("stream-user");
        var firstTask = server.HandleConnectionAsync(firstSender, sharedPrincipal, CancellationToken.None);
        var secondTask = server.HandleConnectionAsync(secondSender, sharedPrincipal, CancellationToken.None);
        var recipientTask = server.HandleConnectionAsync(
            recipient,
            CreateUserPrincipal("stream-recipient"),
            CancellationToken.None);

        firstSender.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "sender-a", "SenderA")));
        secondSender.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "sender-b", "SenderB")));
        recipient.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "shared-recipient", "Recipient")));
        await Task.WhenAll(
            firstSender.WaitForSentFramesAsync(1),
            secondSender.WaitForSentFramesAsync(1),
            recipient.WaitForSentFramesAsync(1));

        var recipientHash = BoltCodec.Fnv1aHash("shared-recipient");
        var firstStreamId = Guid.NewGuid();
        firstSender.Enqueue(WriteFrame(writer => BoltCodec.WriteStreamOpen(
            writer,
            firstStreamId,
            recipientHash,
            BoltCodec.Fnv1aHash("stream"))));
        await recipient.WaitForSentFramesAsync(2);

        secondSender.Enqueue(WriteFrame(writer => BoltCodec.WriteStreamOpen(
            writer,
            Guid.NewGuid(),
            recipientHash,
            BoltCodec.Fnv1aHash("stream"))));
        await Task.Delay(50);
        recipient.SentFrames.Should().HaveCount(2);

        firstSender.Enqueue(WriteFrame(writer => BoltCodec.WriteStreamClose(writer, firstStreamId)));
        await recipient.WaitForSentFramesAsync(3);
        secondSender.Enqueue(WriteFrame(writer => BoltCodec.WriteStreamOpen(
            writer,
            Guid.NewGuid(),
            recipientHash,
            BoltCodec.Fnv1aHash("stream"))));
        await recipient.WaitForSentFramesAsync(4);

        firstSender.Complete();
        secondSender.Complete();
        recipient.Complete();
        await Task.WhenAll(firstTask, secondTask, recipientTask).WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Test]
    public async Task SubscriptionLimit_AllowsFirstTopicAndRejectsSecond()
    {
        var authorizer = new CountingAllowAuthorizer();
        using var server = new BoltServer(
            NullLogger<BoltServer>.Instance,
            new BoltServerOptions { MaxSubscriptionsPerPrincipal = 1 },
            [authorizer]);
        await using var subscriber = new ChannelBoltConnection();
        await using var publisher = new ChannelBoltConnection();
        var subscriberTask = server.HandleConnectionAsync(subscriber, CancellationToken.None);
        var publisherTask = server.HandleConnectionAsync(publisher, CancellationToken.None);

        subscriber.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "subscriber", "Subscriber")));
        publisher.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "publisher", "Publisher")));
        await Task.WhenAll(subscriber.WaitForSentFramesAsync(1), publisher.WaitForSentFramesAsync(1));

        subscriber.Enqueue(WriteFrame(writer => BoltCodec.WriteSubscribe(writer, "allowed.first", "subscriber", durable: false)));
        subscriber.Enqueue(WriteFrame(writer => BoltCodec.WriteSubscribe(writer, "rejected.second", "subscriber", durable: false)));
        await authorizer.WaitForCallsAsync(2);

        var topicNamesField = typeof(BoltServer).GetField(
            "_topicNamesByHash",
            BindingFlags.Instance | BindingFlags.NonPublic);
        topicNamesField.Should().NotBeNull();
        var topicNames = (ConcurrentDictionary<int, string>)topicNamesField!.GetValue(server)!;
        topicNames.Values.Should().Equal("allowed.first");

        publisher.Enqueue(WriteFrame(writer => BoltCodec.WritePublish(writer, "allowed.first", false, [1])));
        publisher.Enqueue(WriteFrame(writer => BoltCodec.WritePublish(writer, "rejected.second", false, [2])));
        await authorizer.WaitForCallsAsync(4);
        await subscriber.WaitForSentFramesAsync(2);
        await Task.Delay(50);

        subscriber.SentFrames.Should().HaveCount(2);
        subscriber.SentFrames.ToArray()[1][0].Should().Be((byte)FrameType.Event);

        subscriber.Complete();
        publisher.Complete();
        await Task.WhenAll(subscriberTask, publisherTask).WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Test]
    public async Task DurableAck_DeniedByAuthorizer_DoesNotAdvanceQueue()
    {
        var authorizer = new DenyAckAuthorizer();
        var durableOptions = Options.Create(new DurableQueueOptions());
        var durableStore = new InMemoryDurableQueueStore(
            durableOptions,
            NullLogger<InMemoryDurableQueueStore>.Instance);
        using var server = new BoltServer(
            NullLogger<BoltServer>.Instance,
            new BoltServerOptions(),
            durableStore,
            durableOptions,
            [authorizer]);
        await using var subscriber = new ChannelBoltConnection();
        var connectionTask = server.HandleConnectionAsync(subscriber, CancellationToken.None);

        subscriber.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "ack-subscriber", "AckSubscriber")));
        await subscriber.WaitForSentFramesAsync(1);

        const string topic = "durable.ack.authorization";
        const string subscriberId = "ack-subscriber-id";
        var topicHash = BoltCodec.Fnv1aHash(topic);
        subscriber.Enqueue(WriteFrame(writer => BoltCodec.WriteSubscribe(
            writer,
            topic,
            subscriberId,
            durable: true)));
        await WaitForDurableBindingAsync(server, topic, subscriberId, "ack-subscriber");
        var sequence = await durableStore.AppendAsync(topicHash, subscriberId, new byte[] { 1 });

        subscriber.Enqueue(WriteFrame(writer => BoltCodec.WriteAck(
            writer,
            topic,
            subscriberId,
            sequence,
            actorAccessToken: "revoked-token")));
        await authorizer.AckObserved.WaitAsync(TimeSpan.FromSeconds(3));
        await Task.Delay(20);

        (await durableStore.GetLastAckedSequenceAsync(topicHash, subscriberId)).Should().Be(0);

        subscriber.Complete();
        await connectionTask.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Test]
    public async Task DurableAck_OldOwnerReplacedDuringAuthorization_DoesNotAdvanceQueue()
    {
        var authorizer = new DelayedAckAuthorizer();
        var durableOptions = Options.Create(new DurableQueueOptions());
        var durableStore = new InMemoryDurableQueueStore(
            durableOptions,
            NullLogger<InMemoryDurableQueueStore>.Instance);
        using var server = new BoltServer(
            NullLogger<BoltServer>.Instance,
            new BoltServerOptions(),
            durableStore,
            durableOptions,
            [authorizer]);
        await using var first = new ChannelBoltConnection();
        await using var second = new ChannelBoltConnection();
        var firstTask = server.HandleConnectionAsync(first, CancellationToken.None);
        var secondTask = server.HandleConnectionAsync(second, CancellationToken.None);

        first.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "ack-first", "AckFirst")));
        second.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "ack-second", "AckSecond")));
        await Task.WhenAll(first.WaitForSentFramesAsync(1), second.WaitForSentFramesAsync(1));

        const string topic = "durable.ack.replacement";
        const string subscriberId = "ack-replacement-id";
        var topicHash = BoltCodec.Fnv1aHash(topic);
        first.Enqueue(WriteFrame(writer => BoltCodec.WriteSubscribe(writer, topic, subscriberId, durable: true)));
        await WaitForDurableBindingAsync(server, topic, subscriberId, "ack-first");
        var sequence = await durableStore.AppendAsync(topicHash, subscriberId, new byte[] { 1 });

        first.Enqueue(WriteFrame(writer => BoltCodec.WriteAck(writer, topic, subscriberId, sequence, "token")));
        await authorizer.AckStarted.WaitAsync(TimeSpan.FromSeconds(3));
        second.Enqueue(WriteFrame(writer => BoltCodec.WriteSubscribe(writer, topic, subscriberId, durable: true)));
        await WaitForDurableBindingAsync(server, topic, subscriberId, "ack-second");
        authorizer.ReleaseAck();
        await Task.Delay(50);

        (await durableStore.GetLastAckedSequenceAsync(topicHash, subscriberId)).Should().Be(0);

        first.Complete();
        second.Complete();
        await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Test]
    public async Task DurableSubscriptionReplacement_KeepsExactlyOnePrincipalReservation()
    {
        var authorizer = new CountingAllowAuthorizer();
        var durableOptions = Options.Create(new DurableQueueOptions());
        var durableStore = new InMemoryDurableQueueStore(
            durableOptions,
            NullLogger<InMemoryDurableQueueStore>.Instance);
        using var server = new BoltServer(
            NullLogger<BoltServer>.Instance,
            new BoltServerOptions { MaxSubscriptionsPerPrincipal = 1 },
            durableStore,
            durableOptions,
            [authorizer]);
        await using var first = new ChannelBoltConnection();
        await using var second = new ChannelBoltConnection();
        var firstTask = server.HandleConnectionAsync(
            first,
            CreateUserPrincipal("principal-a"),
            CancellationToken.None);
        var secondTask = server.HandleConnectionAsync(
            second,
            CreateUserPrincipal("principal-b"),
            CancellationToken.None);

        first.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "durable-a", "DurableA")));
        second.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "durable-b", "DurableB")));
        await Task.WhenAll(first.WaitForSentFramesAsync(1), second.WaitForSentFramesAsync(1));

        const string topic = "durable.topic";
        const string subscriberId = "durable-shared";
        first.Enqueue(WriteFrame(writer => BoltCodec.WriteSubscribe(writer, topic, subscriberId, durable: true)));
        await WaitForDurableBindingAsync(server, topic, subscriberId, "durable-a");
        second.Enqueue(WriteFrame(writer => BoltCodec.WriteSubscribe(writer, topic, subscriberId, durable: true)));
        await WaitForDurableBindingAsync(server, topic, subscriberId, "durable-b");
        first.Enqueue(WriteFrame(writer => BoltCodec.WriteSubscribe(writer, topic, subscriberId, durable: true)));
        await WaitForDurableBindingAsync(server, topic, subscriberId, "durable-a");

        var countsField = typeof(BoltServer).GetField(
            "_subscriptionsByPrincipal",
            BindingFlags.Instance | BindingFlags.NonPublic);
        countsField.Should().NotBeNull();
        var counts = (ConcurrentDictionary<string, int>)countsField!.GetValue(server)!;
        counts.Should().ContainSingle();
        counts.Should().Contain("principal:principal-a", 1);

        first.Complete();
        second.Complete();
        await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Test]
    public async Task DurablePermanentUnsubscribe_WhenDetached_RemovesOfflineSubscription()
    {
        var authorizer = new CountingAllowAuthorizer();
        var durableOptions = Options.Create(new DurableQueueOptions());
        var durableStore = new InMemoryDurableQueueStore(
            durableOptions,
            NullLogger<InMemoryDurableQueueStore>.Instance);
        using var server = new BoltServer(
            NullLogger<BoltServer>.Instance,
            new BoltServerOptions(),
            durableStore,
            durableOptions,
            [authorizer]);
        await using var connection = new ChannelBoltConnection();
        var connectionTask = server.HandleConnectionAsync(connection, CancellationToken.None);

        connection.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "offline-owner", "OfflineOwner")));
        await connection.WaitForSentFramesAsync(1);
        const string topic = "durable.offline.unregister";
        const string subscriberId = "offline-subscriber";
        var topicHash = BoltCodec.Fnv1aHash(topic);
        connection.Enqueue(WriteFrame(writer => BoltCodec.WriteSubscribe(
            writer,
            topic,
            subscriberId,
            durable: true)));
        await WaitForDurableBindingAsync(server, topic, subscriberId, "offline-owner");
        await durableStore.AppendAsync(topicHash, subscriberId, new byte[] { 1 });

        connection.Enqueue(WriteFrame(writer => BoltCodec.WriteUnsubscribe(
            writer,
            topic,
            subscriberId,
            permanent: false)));
        await WaitForNoDurableBindingAsync(server, topicHash, subscriberId);
        (await durableStore.IsDurableSubscriberRegisteredAsync(topicHash, subscriberId)).Should().BeTrue();

        connection.Enqueue(WriteFrame(writer => BoltCodec.WriteUnsubscribe(
            writer,
            topic,
            subscriberId,
            permanent: true)));
        connection.Enqueue(WriteFrame(writer => BoltCodec.WritePublish(
            writer,
            "durable.unregister.barrier",
            false,
            [2])));
        await authorizer.WaitForCallsAsync(4);
        (await durableStore.IsDurableSubscriberRegisteredAsync(topicHash, subscriberId)).Should().BeFalse();
        (await durableStore.GetLastAckedSequenceAsync(topicHash, subscriberId)).Should().Be(0);

        connection.Complete();
        await connectionTask.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Test]
    public async Task DurablePermanentUnsubscribe_WhenAnotherSessionOwnsSubscription_DoesNotRemoveIt()
    {
        var authorizer = new CountingAllowAuthorizer();
        var durableOptions = Options.Create(new DurableQueueOptions());
        var durableStore = new InMemoryDurableQueueStore(
            durableOptions,
            NullLogger<InMemoryDurableQueueStore>.Instance);
        using var server = new BoltServer(
            NullLogger<BoltServer>.Instance,
            new BoltServerOptions(),
            durableStore,
            durableOptions,
            [authorizer]);
        await using var owner = new ChannelBoltConnection();
        await using var other = new ChannelBoltConnection();
        var ownerTask = server.HandleConnectionAsync(owner, CancellationToken.None);
        var otherTask = server.HandleConnectionAsync(other, CancellationToken.None);

        owner.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "current-owner", "CurrentOwner")));
        other.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "other-session", "OtherSession")));
        await Task.WhenAll(owner.WaitForSentFramesAsync(1), other.WaitForSentFramesAsync(1));
        const string topic = "durable.unregister.ownership";
        const string subscriberId = "owned-subscriber";
        var topicHash = BoltCodec.Fnv1aHash(topic);
        owner.Enqueue(WriteFrame(writer => BoltCodec.WriteSubscribe(
            writer,
            topic,
            subscriberId,
            durable: true)));
        await WaitForDurableBindingAsync(server, topic, subscriberId, "current-owner");

        other.Enqueue(WriteFrame(writer => BoltCodec.WriteUnsubscribe(
            writer,
            topic,
            subscriberId,
            permanent: true)));
        other.Enqueue(WriteFrame(writer => BoltCodec.WritePublish(
            writer,
            "durable.unregister.ownership.barrier",
            false,
            [2])));
        await authorizer.WaitForCallsAsync(3);

        GetDurableBinding(server, topicHash, subscriberId).ClientId.Should().Be("current-owner");
        (await durableStore.IsDurableSubscriberRegisteredAsync(topicHash, subscriberId)).Should().BeTrue();

        owner.Complete();
        other.Complete();
        await Task.WhenAll(ownerTask, otherTask).WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Test]
    public async Task DurableReplayReplacement_ObsoleteReplayCannotDrainNewOwnerDeferredEvents()
    {
        var authorizer = new CountingAllowAuthorizer();
        var durableOptions = Options.Create(new DurableQueueOptions());
        var durableStore = new ControlledReplayDurableStore(durableOptions);
        using var server = new BoltServer(
            NullLogger<BoltServer>.Instance,
            new BoltServerOptions { MaxSubscriptionsPerPrincipal = 1 },
            durableStore,
            durableOptions,
            [authorizer]);
        await using var first = new ChannelBoltConnection();
        await using var second = new ChannelBoltConnection();
        await using var publisher = new ChannelBoltConnection();
        var firstTask = server.HandleConnectionAsync(first, CreateUserPrincipal("replay-first"), CancellationToken.None);
        var secondTask = server.HandleConnectionAsync(second, CreateUserPrincipal("replay-second"), CancellationToken.None);
        var publisherTask = server.HandleConnectionAsync(publisher, CreateUserPrincipal("replay-publisher"), CancellationToken.None);

        first.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "replay-first", "ReplayFirst")));
        second.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "replay-second", "ReplaySecond")));
        publisher.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "replay-publisher", "ReplayPublisher")));
        await Task.WhenAll(
            first.WaitForSentFramesAsync(1),
            second.WaitForSentFramesAsync(1),
            publisher.WaitForSentFramesAsync(1));

        const string topic = "durable.replay.owner";
        const string subscriberId = "shared-replay";
        var topicHash = BoltCodec.Fnv1aHash(topic);
        first.Enqueue(WriteFrame(writer => BoltCodec.WriteSubscribe(writer, topic, subscriberId, durable: true)));
        await WaitForDurableBindingAsync(server, topic, subscriberId, "replay-first");
        await durableStore.FirstReplayStarted.WaitAsync(TimeSpan.FromSeconds(3));
        var obsoleteConnection = GetDurableBinding(server, topicHash, subscriberId);

        second.Enqueue(WriteFrame(writer => BoltCodec.WriteSubscribe(writer, topic, subscriberId, durable: true)));
        await WaitForDurableBindingAsync(server, topic, subscriberId, "replay-second");
        await durableStore.SecondReplayStarted.WaitAsync(TimeSpan.FromSeconds(3));

        publisher.Enqueue(WriteFrame(writer => BoltCodec.WritePublish(writer, topic, false, [42])));
        await WaitForDeferredEventCountAsync(server, topicHash, subscriberId, 1);

        var completeReplay = typeof(BoltServer).GetMethod(
            "CompleteDurableReplayAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        completeReplay.Should().NotBeNull();
        await ((Task)completeReplay!.Invoke(
            server,
            [obsoleteConnection, topicHash, subscriberId, 0L, CancellationToken.None])!).WaitAsync(
            TimeSpan.FromSeconds(3));

        durableStore.ReleaseSecondReplay();
        await second.WaitForSentFramesAsync(2);
        second.SentFrames.ToArray()[1][0].Should().Be((byte)FrameType.Event);

        durableStore.ReleaseFirstReplay();
        first.Complete();
        second.Complete();
        publisher.Complete();
        await Task.WhenAll(firstTask, secondTask, publisherTask).WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Test]
    public async Task DurableDisconnectCleanup_GateTimeoutDoesNotMutateWithoutOwnership()
    {
        var authorizer = new CountingAllowAuthorizer();
        var cleanupLogger = new DurableCleanupTimeoutLogger();
        var durableOptions = Options.Create(new DurableQueueOptions());
        var durableStore = new InMemoryDurableQueueStore(
            durableOptions,
            NullLogger<InMemoryDurableQueueStore>.Instance);
        using var server = new BoltServer(
            cleanupLogger,
            new BoltServerOptions
            {
                MaxSubscriptionsPerPrincipal = 1,
                TransportCloseTimeoutMs = 25
            },
            durableStore,
            durableOptions,
            [authorizer]);
        await using var connection = new ChannelBoltConnection();
        var connectionTask = server.HandleConnectionAsync(
            connection,
            CreateUserPrincipal("cleanup-owner"),
            CancellationToken.None);

        connection.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "cleanup-owner", "CleanupOwner")));
        await connection.WaitForSentFramesAsync(1);
        const string topic = "durable.cleanup.gate";
        const string subscriberId = "cleanup-shared";
        var topicHash = BoltCodec.Fnv1aHash(topic);
        connection.Enqueue(WriteFrame(writer => BoltCodec.WriteSubscribe(writer, topic, subscriberId, durable: true)));
        await WaitForDurableBindingAsync(server, topic, subscriberId, "cleanup-owner");

        var gate = GetDurableSubscriptionGate(server, topicHash, subscriberId);
        await gate.WaitAsync();
        try
        {
            connection.Complete();
            await cleanupLogger.WaitForTimeoutAsync(TimeSpan.FromSeconds(10));
            await connectionTask.WaitAsync(TimeSpan.FromSeconds(3));

            GetDurableBinding(server, topicHash, subscriberId).ClientId.Should().Be("cleanup-owner");
            GetSubscriptionCounts(server).Should().Contain("principal:cleanup-owner", 1);
        }
        finally
        {
            gate.Release();
        }

        await WaitForNoDurableBindingAsync(server, topicHash, subscriberId);
        GetSubscriptionCounts(server).Should().BeEmpty();
    }

    [Test]
    public async Task MediaStreamLimit_RejectsExcessConfigForConnection()
    {
        using var server = new BoltServer(
            NullLogger<BoltServer>.Instance,
            new BoltServerOptions
            {
                MediaEnabled = true,
                MaxMediaStreamsPerPrincipal = 1
            });
        await using var caller = new ChannelBoltConnection();
        await using var callee = new ChannelBoltConnection();
        var callerTask = server.HandleConnectionAsync(caller, CancellationToken.None);
        var calleeTask = server.HandleConnectionAsync(callee, CancellationToken.None);

        caller.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "media-caller", "MediaCaller")));
        callee.Enqueue(WriteFrame(writer => BoltCodec.WriteRegister(writer, "media-callee", "MediaCallee")));
        await Task.WhenAll(caller.WaitForSentFramesAsync(1), callee.WaitForSentFramesAsync(1));

        var callId = Guid.NewGuid();
        var initiatePayload = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(initiatePayload, BoltCodec.Fnv1aHash("media-callee"));
        caller.Enqueue(WriteFrame(writer => BoltCodec.WriteCallSignal(
            writer,
            callId,
            SignalType.Initiate,
            initiatePayload)));
        await Task.WhenAll(caller.WaitForSentFramesAsync(2), callee.WaitForSentFramesAsync(2));

        callee.Enqueue(WriteFrame(writer => BoltCodec.WriteCallSignal(
            writer,
            callId,
            SignalType.Answer,
            ReadOnlySpan<byte>.Empty)));
        await caller.WaitForSentFramesAsync(3);

        caller.Enqueue(WriteMediaConfig(callId, Guid.NewGuid()));
        await callee.WaitForSentFramesAsync(3);
        caller.Enqueue(WriteMediaConfig(callId, Guid.NewGuid()));
        await Task.Delay(50);

        callee.SentFrames.Should().HaveCount(3);

        caller.Complete();
        callee.Complete();
        await Task.WhenAll(callerTask, calleeTask).WaitAsync(TimeSpan.FromSeconds(3));
    }

    private static byte[] WriteFrame(Action<IBufferWriter<byte>> write)
    {
        var writer = new ArrayBufferWriter<byte>();
        write(writer);
        return writer.WrittenSpan.ToArray();
    }

    private static void AssertRegisterAck(byte[] frame, bool expectedSuccess)
    {
        BoltCodec.TryReadRegisterAck(frame, out var success, out var version).Should().BeTrue();
        success.Should().Be(expectedSuccess);
        version.Should().Be(BoltCodec.WireVersion);
    }

    private static byte[] WriteMediaConfig(Guid callId, Guid streamId) =>
        WriteFrame(writer => BoltCodec.WriteMediaConfig(
            writer,
            streamId,
            callId,
            MediaType.Audio,
            CodecId.Opus,
            48_000,
            1,
            64,
            0,
            ReadOnlySpan<byte>.Empty));

    private static ClaimsPrincipal CreateUserPrincipal(string subject) =>
        new(new ClaimsIdentity([new Claim("sub", subject)], "test"));

    private static ClaimsPrincipal CreateServicePrincipal(string serviceName) =>
        new(new ClaimsIdentity(
            [
                new Claim("service", serviceName),
                new Claim("scope", "bolt.service")
            ],
            "test"));

    private static string Sha256Hex(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    private static async Task WaitForDurableBindingAsync(
        BoltServer server,
        string topic,
        string subscriberId,
        string expectedClientId)
    {
        var bindingsField = typeof(BoltServer).GetField(
            "_liveDurableConnections",
            BindingFlags.Instance | BindingFlags.NonPublic);
        bindingsField.Should().NotBeNull();
        var bindings = (ConcurrentDictionary<(int TopicHash, string SubscriberId), BoltHubConnection>)
            bindingsField!.GetValue(server)!;
        var key = (BoltCodec.Fnv1aHash(topic), subscriberId);
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            if (bindings.TryGetValue(key, out var connection) &&
                string.Equals(connection.ClientId, expectedClientId, StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.Fail($"Durable subscriber was not bound to '{expectedClientId}'.");
    }

    private static BoltHubConnection GetDurableBinding(
        BoltServer server,
        int topicHash,
        string subscriberId)
    {
        var bindingsField = typeof(BoltServer).GetField(
            "_liveDurableConnections",
            BindingFlags.Instance | BindingFlags.NonPublic);
        bindingsField.Should().NotBeNull();
        var bindings = (ConcurrentDictionary<(int TopicHash, string SubscriberId), BoltHubConnection>)
            bindingsField!.GetValue(server)!;
        return bindings[(topicHash, subscriberId)];
    }

    private static ConcurrentDictionary<string, int> GetSubscriptionCounts(BoltServer server)
    {
        var countsField = typeof(BoltServer).GetField(
            "_subscriptionsByPrincipal",
            BindingFlags.Instance | BindingFlags.NonPublic);
        countsField.Should().NotBeNull();
        return (ConcurrentDictionary<string, int>)countsField!.GetValue(server)!;
    }

    private static SemaphoreSlim GetDurableSubscriptionGate(
        BoltServer server,
        int topicHash,
        string subscriberId)
    {
        var getGate = typeof(BoltServer).GetMethod(
            "GetDurableSubscriptionGate",
            BindingFlags.Instance | BindingFlags.NonPublic);
        getGate.Should().NotBeNull();
        return (SemaphoreSlim)getGate!.Invoke(server, [(topicHash, subscriberId)])!;
    }

    private static async Task WaitForNoDurableBindingAsync(
        BoltServer server,
        int topicHash,
        string subscriberId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                _ = GetDurableBinding(server, topicHash, subscriberId);
            }
            catch (KeyNotFoundException)
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.Fail("Durable subscriber binding was not cleaned up.");
    }

    private static async Task WaitForDeferredEventCountAsync(
        BoltServer server,
        int topicHash,
        string subscriberId,
        int expected)
    {
        var statesField = typeof(BoltServer).GetField(
            "_replayingDurableSubscriptions",
            BindingFlags.Instance | BindingFlags.NonPublic);
        statesField.Should().NotBeNull();
        var states = statesField!.GetValue(server)!;
        var tryGetValue = states.GetType().GetMethod("TryGetValue");
        tryGetValue.Should().NotBeNull();
        var key = (topicHash, subscriberId);
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            object?[] arguments = [key, null];
            if ((bool)tryGetValue!.Invoke(states, arguments)! && arguments[1] is { } state)
            {
                var queue = state.GetType().GetProperty("DeferredEvents")!.GetValue(state)!;
                var count = (int)queue.GetType().GetProperty("Count")!.GetValue(queue)!;
                if (count >= expected)
                    return;
            }

            await Task.Delay(10);
        }

        Assert.Fail($"Durable replay did not defer {expected} event(s).");
    }

    private sealed class ThrowingTopicAuthorizer : IBoltTopicAuthorizer
    {
        private int _callCount;
        public int CallCount => Volatile.Read(ref _callCount);

        public ValueTask<bool> AuthorizeAsync(BoltTopicAuthorizationContext context, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _callCount);
            throw new InvalidOperationException("Authorization dependency failed");
        }

        public async Task WaitForCallsAsync(int expected)
        {
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (CallCount < expected && DateTime.UtcNow < deadline)
                await Task.Delay(10);

            CallCount.Should().BeGreaterThanOrEqualTo(expected);
        }
    }

    private sealed class CountingAllowAuthorizer : IBoltTopicAuthorizer
    {
        private readonly TaskCompletionSource _callsChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;

        public ValueTask<bool> AuthorizeAsync(BoltTopicAuthorizationContext context, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _callCount);
            _callsChanged.TrySetResult();
            return ValueTask.FromResult(true);
        }

        public async Task WaitForCallsAsync(int expected)
        {
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (Volatile.Read(ref _callCount) < expected && DateTime.UtcNow < deadline)
            {
                await _callsChanged.Task.WaitAsync(TimeSpan.FromSeconds(1));
                await Task.Delay(10);
            }

            Volatile.Read(ref _callCount).Should().BeGreaterThanOrEqualTo(expected);
        }
    }

    private sealed class DenyAckAuthorizer : IBoltTopicAuthorizer
    {
        private readonly TaskCompletionSource _ackObserved =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task AckObserved => _ackObserved.Task;

        public ValueTask<bool> AuthorizeAsync(
            BoltTopicAuthorizationContext context,
            CancellationToken ct = default)
        {
            if (context.Operation != BoltTopicOperation.Ack)
                return ValueTask.FromResult(true);

            _ackObserved.TrySetResult();
            return ValueTask.FromResult(false);
        }
    }

    private sealed class DelayedAckAuthorizer : IBoltTopicAuthorizer
    {
        private readonly TaskCompletionSource _ackStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseAck =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task AckStarted => _ackStarted.Task;

        public void ReleaseAck() => _releaseAck.TrySetResult();

        public async ValueTask<bool> AuthorizeAsync(
            BoltTopicAuthorizationContext context,
            CancellationToken ct = default)
        {
            if (context.Operation != BoltTopicOperation.Ack)
                return true;

            _ackStarted.TrySetResult();
            await _releaseAck.Task.WaitAsync(ct);
            return true;
        }
    }

    private sealed class DurableCleanupTimeoutLogger : ILogger<BoltServer>
    {
        private const string CleanupTimeoutMessage =
            "Durable subscription cleanup gate timed out; cleanup will retry.";

        private readonly TaskCompletionSource _timeoutLogged =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning &&
                formatter(state, exception).StartsWith(CleanupTimeoutMessage, StringComparison.Ordinal))
            {
                _timeoutLogged.TrySetResult();
            }
        }

        public Task WaitForTimeoutAsync(TimeSpan timeout) => _timeoutLogged.Task.WaitAsync(timeout);
    }

    private sealed class ControlledReplayDurableStore(IOptions<DurableQueueOptions> options) : IDurableQueueStore
    {
        private readonly InMemoryDurableQueueStore _inner = new(
            options,
            NullLogger<InMemoryDurableQueueStore>.Instance);
        private readonly TaskCompletionSource _firstReplayStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _secondReplayStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseFirstReplay = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseSecondReplay = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _replayCalls;

        public Task FirstReplayStarted => _firstReplayStarted.Task;
        public Task SecondReplayStarted => _secondReplayStarted.Task;

        public void ReleaseFirstReplay() => _releaseFirstReplay.TrySetResult();
        public void ReleaseSecondReplay() => _releaseSecondReplay.TrySetResult();

        public Task<long> AppendAsync(
            int topicHash,
            string subscriberId,
            ReadOnlyMemory<byte> payload,
            CancellationToken ct = default) =>
            _inner.AppendAsync(topicHash, subscriberId, payload, ct);

        public IAsyncEnumerable<(long Sequence, byte[] Payload)> ReadFromAsync(
            int topicHash,
            string subscriberId,
            long fromSequence,
            int maxCount,
            CancellationToken ct = default) =>
            _inner.ReadFromAsync(topicHash, subscriberId, fromSequence, maxCount, ct);

        public Task AckAsync(
            int topicHash,
            string subscriberId,
            long upToSequence,
            CancellationToken ct = default) =>
            _inner.AckAsync(topicHash, subscriberId, upToSequence, ct);

        public Task RegisterDurableSubscriberAsync(
            int topicHash,
            string subscriberId,
            CancellationToken ct = default) =>
            _inner.RegisterDurableSubscriberAsync(topicHash, subscriberId, ct);

        public Task<bool> TryRegisterDurableSubscriberAsync(
            int topicHash,
            string subscriberId,
            int maxSubscribers,
            CancellationToken ct = default) =>
            _inner.TryRegisterDurableSubscriberAsync(topicHash, subscriberId, maxSubscribers, ct);

        public Task UnregisterDurableSubscriberAsync(
            int topicHash,
            string subscriberId,
            CancellationToken ct = default) =>
            _inner.UnregisterDurableSubscriberAsync(topicHash, subscriberId, ct);

        public Task<IReadOnlyList<string>> GetDurableSubscribersAsync(
            int topicHash,
            CancellationToken ct = default) =>
            _inner.GetDurableSubscribersAsync(topicHash, ct);

        public Task<bool> IsDurableSubscriberRegisteredAsync(
            int topicHash,
            string subscriberId,
            CancellationToken ct = default) =>
            _inner.IsDurableSubscriberRegisteredAsync(topicHash, subscriberId, ct);

        public async Task<long> GetLastAckedSequenceAsync(
            int topicHash,
            string subscriberId,
            CancellationToken ct = default)
        {
            var call = Interlocked.Increment(ref _replayCalls);
            if (call == 1)
            {
                _firstReplayStarted.TrySetResult();
                await _releaseFirstReplay.Task.WaitAsync(ct);
            }
            else if (call == 2)
            {
                _secondReplayStarted.TrySetResult();
                await _releaseSecondReplay.Task.WaitAsync(ct);
            }

            return await _inner.GetLastAckedSequenceAsync(topicHash, subscriberId, ct);
        }
    }

    private class ScriptedBoltConnection(params byte[][] frames) : IBoltConnection
    {
        private readonly Queue<byte[]> _frames = new(frames);

        public ConcurrentQueue<byte[]> SentFrames { get; } = new();
        public bool SupportsDatagrams => false;
        public bool IsConnected { get; protected set; } = true;
        public BoltTransport TransportType => BoltTransport.WebSocket;

        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            SentFrames.Enqueue(data.ToArray());
            return ValueTask.CompletedTask;
        }

        public virtual ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(
            Memory<byte> buffer,
            CancellationToken ct = default)
        {
            if (_frames.TryDequeue(out var frame))
            {
                frame.CopyTo(buffer);
                return ValueTask.FromResult((frame.Length, true));
            }

            IsConnected = false;
            return ValueTask.FromResult((0, true));
        }

        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
            ValueTask.CompletedTask;

        public virtual ValueTask CloseAsync(CancellationToken ct = default)
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }

        public virtual ValueTask DisposeAsync()
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class BlockingBoltConnection : ScriptedBoltConnection
    {
        public bool CloseCalled { get; private set; }

        public override async ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(
            Memory<byte> buffer,
            CancellationToken ct = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return (0, true);
        }

        public override ValueTask CloseAsync(CancellationToken ct = default)
        {
            CloseCalled = true;
            IsConnected = false;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class NonResponsiveCloseBoltConnection : ScriptedBoltConnection
    {
        public bool CloseCalled { get; private set; }
        public bool DisposeCalled { get; private set; }

        public override async ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(
            Memory<byte> buffer,
            CancellationToken ct = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return (0, true);
        }

        public override async ValueTask CloseAsync(CancellationToken ct = default)
        {
            CloseCalled = true;
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        }

        public override ValueTask DisposeAsync()
        {
            DisposeCalled = true;
            IsConnected = false;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ChannelBoltConnection : IBoltConnection
    {
        private readonly Channel<byte[]> _incoming = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        public ConcurrentQueue<byte[]> SentFrames { get; } = new();
        public bool SupportsDatagrams => false;
        public bool IsConnected { get; private set; } = true;
        public BoltTransport TransportType => BoltTransport.WebSocket;

        public void Enqueue(byte[] frame) => _incoming.Writer.TryWrite(frame).Should().BeTrue();

        public void Complete() => _incoming.Writer.TryComplete();

        public async Task WaitForSentFramesAsync(int expected)
        {
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (SentFrames.Count < expected && DateTime.UtcNow < deadline)
                await Task.Delay(10);

            SentFrames.Count.Should().BeGreaterThanOrEqualTo(expected);
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
            while (await _incoming.Reader.WaitToReadAsync(ct))
            {
                if (!_incoming.Reader.TryRead(out var frame))
                    continue;

                frame.CopyTo(buffer);
                return (frame.Length, true);
            }

            IsConnected = false;
            return (0, true);
        }

        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
            ValueTask.CompletedTask;

        public ValueTask CloseAsync(CancellationToken ct = default)
        {
            Complete();
            IsConnected = false;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Complete();
            IsConnected = false;
            return ValueTask.CompletedTask;
        }
    }
}
