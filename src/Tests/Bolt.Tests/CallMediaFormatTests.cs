using Bolt.Media.Browser;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Bolt.Tests;

/// <summary>
/// Compact SFrame frames and long Opus packets are used only when every remote member of the epoch said, in its
/// authenticated key envelope, that it reads them. A call with an older client keeps the formats that client reads.
/// </summary>
[TestFixture]
public sealed class CallMediaFormatTests
{
    [Test]
    public void TheCommonFormat_IsWhatEveryPeerReads()
    {
        CallMediaFormat.Common([]).Should().Be(CallMediaFormat.Legacy, "nobody announced anything");
        CallMediaFormat.Common([0]).Should().Be(CallMediaFormat.Legacy, "an envelope without the field is an older client");
        CallMediaFormat.Common([CallMediaFormat.Compact, 0]).Should().Be(CallMediaFormat.Legacy, "one older client decides for the call");
        CallMediaFormat.Common([CallMediaFormat.Compact, CallMediaFormat.Compact]).Should().Be(CallMediaFormat.Compact);
        CallMediaFormat.Common([99]).Should().Be(CallMediaFormat.Current, "a newer client is read at what this build knows");
        CallMediaFormat.MaxAudioFrameMs(CallMediaFormat.Legacy).Should().Be(20);
        CallMediaFormat.MaxAudioFrameMs(CallMediaFormat.Compact).Should().Be(60);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task InstallingAnEpoch_TellsTheSessionWhetherToSendCompactFrames(bool compact)
    {
        var js = Substitute.For<Microsoft.JSInterop.IJSRuntime>();
        var module = Substitute.For<Microsoft.JSInterop.IJSObjectReference>();
        var session = Substitute.For<Microsoft.JSInterop.IJSObjectReference>();
        js.InvokeAsync<Microsoft.JSInterop.IJSObjectReference>("import", Arg.Any<object?[]>()).Returns(module);
        module.InvokeAsync<Microsoft.JSInterop.IJSObjectReference>("createSession", Arg.Any<object?[]>()).Returns(session);
        await using var bridge = new BoltSFrameInterop(js);
        await bridge.ConfigureAsync(Guid.NewGuid(), "alice");

        await bridge.InstallEpochAsync("1", new string('a', 64), new("alice", "10", new byte[32]), [new("bob", "11", new byte[32])], compact);

        var install = session.ReceivedCalls().Single(call => call.GetArguments()[0] as string == "installEpoch");
        var epoch = ((object?[])install.GetArguments()[1]!)[0]!;
        epoch.GetType().GetProperty("compact")!.GetValue(epoch).Should().Be(compact);
        bridge.Compact.Should().Be(compact);
    }
}
