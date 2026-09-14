using Bolt.Media.Browser;
using FluentAssertions;
using Microsoft.JSInterop;
using NSubstitute;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public sealed class BoltSFrameInteropTests
{
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
