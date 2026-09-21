using Bolt.Media.Browser;
using FluentAssertions;
using Microsoft.JSInterop;
using NSubstitute;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public sealed class BoltSFrameInteropTests
{
    [TestCase(false, 96)]
    [TestCase(true, 32)]
    public async Task VideoFragmentBurst_OrderedIngressVersusPreviousFanOut(bool previousFanOut, int expected)
    {
        var (bridge, session) = Create(); await using var owned = bridge;
        var call = Guid.NewGuid();
        await bridge.ConfigureAsync(call, "alice");
        await bridge.InstallEpochAsync("1", new string('a', 64), new("alice", "1", new byte[32]),
            [new("bob", "2", new byte[32])]);
        await bridge.ActivateEpochAsync("1", new string('a', 64));
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.InvokeAsync<byte[]>("decrypt", Arg.Any<object?[]>()).Returns(args =>
            new ValueTask<byte[]>(Decrypt((byte[])((object?[])args[1]!)[1]!)));
        async Task<byte[]> Decrypt(byte[] data) { await held.Task; return data; }
        var connection = new Bolt.Client.BoltConnection(new NoopConnection());
        await using var stream = new Bolt.Media.BoltMediaStream(connection, Guid.NewGuid(), call, false);
        stream.SetEncryption(bridge.ForStream(call, "bob"));
        var enqueue = typeof(Bolt.Media.BoltMediaStream).GetMethod("QueueReceivedFrame",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var pending = new List<Task>();
        for (uint sequence = 0; sequence < 96; sequence++)
        {
            if (previousFanOut) pending.Add(stream.EnqueueFrameAsync(sequence, 90_000u, new byte[4096], 0x10).AsTask());
            else enqueue.Invoke(stream, [sequence, 90_000u, new byte[4096], (byte)0x10]);
        }
        held.SetResult();
        await Task.WhenAll(pending);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = new List<uint>();
        await foreach (var packet in stream.ReadFramesAsync(timeout.Token))
        {
            received.Add(packet.SequenceNumber);
            if (received.Count == expected) break;
        }
        var expectedSequences = Enumerable.Range(0, expected).Select(x => (uint)x);
        if (previousFanOut)
            received.Should().BeEquivalentTo(expectedSequences); // Concurrent continuations can reorder the legacy path.
        else
            received.Should().Equal(expectedSequences);
        TestContext.Out.WriteLine($"96-fragment picture, legacy fan-out={previousFanOut}: {received.Count}/96 delivered.");
        connection.CompleteSendChannel();
    }

    private sealed class NoopConnection : Bolt.Protocol.Transport.IBoltConnection
    {
        public bool SupportsDatagrams => false;
        public bool IsConnected => true;
        public Bolt.Protocol.Transport.BoltTransport TransportType => Bolt.Protocol.Transport.BoltTransport.WebSocket;
        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default)
            => ValueTask.FromResult((0, true));
        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask CloseAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static (BoltSFrameInterop Bridge, IJSObjectReference Session) Create()
    {
        var js = Substitute.For<IJSRuntime>();
        var module = Substitute.For<IJSObjectReference>();
        var session = Substitute.For<IJSObjectReference>();
        js.InvokeAsync<IJSObjectReference>("import", Arg.Any<object?[]>()).Returns(module);
        module.InvokeAsync<IJSObjectReference>("createSession", Arg.Any<object?[]>()).Returns(session);
        return (new BoltSFrameInterop(js), session);
    }

    [Test]
    public async Task StreamProvider_EpochPause_DropsInFlightEncryptionAndBindsTimestamp()
    {
        var (bridge, session) = Create();
        await using var owned = bridge;
        var call = Guid.NewGuid(); var stream = Guid.NewGuid();
        await bridge.ConfigureAsync(call, "alice");
        await bridge.InstallEpochAsync("1", new string('a',64), new("alice","1",new byte[32]),
            [new("bob","2",new byte[32])]);
        var provider = bridge.ForStream(call, "alice");
        provider.IsReady.Should().BeFalse();
        await bridge.ActivateEpochAsync("1", new string('a',64));
        var completion = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.InvokeAsync<byte[]>("encrypt", Arg.Any<object?[]>()).Returns(new ValueTask<byte[]>(completion.Task));
        var pending = provider.EncryptAsync([1,2], 4, 3840, stream);
        var pause = bridge.PauseAsync();
        provider.IsReady.Should().BeFalse();
        var result = new byte[] { 10, 11 };
        completion.SetResult(result);
        await FluentActions.Awaiting(() => pending).Should().ThrowAsync<InvalidOperationException>();
        await pause;
        result.Should().Equal(0, 0);
        await session.Received(1).InvokeAsync<byte[]>("encrypt", Arg.Is<object?[]>(x =>
            (string)x[1]! == stream.ToString("D") && (uint)x[2]! == 4 && (uint)x[3]! == 3840));
    }

    [Test]
    public async Task StreamProvider_NewCall_CannotReuseStaleProvider()
    {
        var (bridge, _) = Create(); await using var owned = bridge;
        var old = Guid.NewGuid();
        await bridge.ConfigureAsync(old, "alice");
        var provider = bridge.ForStream(old, "alice");
        await bridge.EndCallAsync();
        await bridge.ConfigureAsync(Guid.NewGuid(), "alice");
        await bridge.ActivateEpochAsync("new", new string('a',64));
        provider.IsReady.Should().BeFalse();
        await FluentActions.Awaiting(() => provider.EncryptAsync([1], 0, 0, Guid.NewGuid()))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task StreamProvider_RemovedSender_CannotDecryptInCurrentEpoch()
    {
        var (bridge, _) = Create(); await using var owned = bridge;
        var call = Guid.NewGuid(); await bridge.ConfigureAsync(call,"alice");
        var removed = bridge.ForStream(call,"carol");
        await bridge.InstallEpochAsync("2",new string('a',64),new("alice","1",new byte[32]),[new("bob","2",new byte[32])]);
        await bridge.ActivateEpochAsync("2",new string('a',64));
        await FluentActions.Awaiting(() => removed.DecryptAsync([1],0,0,Guid.NewGuid()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*not in this epoch*");
    }

    [Test]
    public async Task StreamProvider_SlowJavaScript_BoundsQueuedFrames()
    {
        var (bridge, session) = Create(); await using var owned = bridge;
        var call = Guid.NewGuid(); await bridge.ConfigureAsync(call,"alice");
        await bridge.ActivateEpochAsync("1",new string('a',64));
        var provider = bridge.ForStream(call,"alice");
        var completion = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.InvokeAsync<byte[]>("encrypt",Arg.Any<object?[]>()).Returns(new ValueTask<byte[]>(completion.Task));
        var pending = Enumerable.Range(0,32).Select(x => provider.EncryptAsync([1],(uint)x,0,Guid.NewGuid())).ToArray();
        await FluentActions.Awaiting(() => provider.EncryptAsync([1],33,0,Guid.NewGuid()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*capacity*");
        completion.SetResult([4]);
        await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(2));
    }
}
