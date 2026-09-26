using System.Net;
using System.Text.RegularExpressions;
using Bolt.Media;
using NUnit.Framework;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Tests;

/// <summary>
/// What the call screen says while a connection is poor, lost, or coming back, rendered from the
/// real component. Reconnecting is calm (a status and a quiet pill, the controls stay), a person who
/// is away keeps their dimmed last picture with their name on it, and a call that ended by itself
/// says why and offers to call back. Status changes go to the one live region, without the clock.
/// </summary>
public sealed partial class CallScreenTests
{
    [Test]
    public async Task Reconnecting_IsACalmStatus_WithItsClockKeptOutOfTheLiveRegion()
    {
        var html = await ReadableAsync("voice-reconnecting", Reconnecting(secondsAgo: 12));
        Assert.Multiple(() =>
        {
            Assert.That(LiveRegion(html), Is.EqualTo("Reconnecting…"), "announced once, in words");
            Assert.That(html, Does.Match("<p class=\"call-status\">Reconnecting…</p>"));
            Assert.That(html, Does.Match("class=\"call-notice link-notice reconnecting\"[^>]*>.*Reconnecting….*<span class=\"t\" aria-hidden=\"true\">00:1[23]</span>"),
                "the elapsed time is visible, and hidden from the screen reader so it is not read every second");
            Assert.That(html, Does.Not.Contain("role=\"alert\""), "nothing about this is an alarm");
            Assert.That(Button(html, "End"), Does.Not.Contain("disabled"), "hanging up is always possible");
            Assert.That(Button(html, "Mute"), Does.Not.Contain("disabled"), "so is muting: the microphone is still ours");
            Assert.That(Button(html, "Turn camera on"), Does.Contain("disabled"), "a camera cannot be offered to a call that is not connected");
        });
    }

    [Test]
    public async Task Reconnecting_CountsDownOnlyAtTheVeryEnd()
    {
        var html = await ReadableAsync("voice-reconnecting-last", Reconnecting(secondsAgo: 38));
        Assert.That(html, Does.Match("<span class=\"t\" aria-hidden=\"true\">Call ends in [67] s</span>"));
    }

    [Test]
    public async Task PoorConnection_IsASubtleNotice()
    {
        var html = await ReadableAsync("voice-poor", state =>
        {
            Voice(connected: true)(state);
            var link = new CallLinkMonitor();
            link.Connected(0);
            for (var i = 0; i < 40; i++) link.Echo(-2_000, 0);
            link.Evaluate(0);
            state.Attempt("Link", link);
            state.Attempt("EverConnected", true);
        });
        Assert.Multiple(() =>
        {
            Assert.That(LiveRegion(html), Is.EqualTo("Poor connection"));
            Assert.That(html, Does.Contain("class=\"call-notice link-notice poor\""));
            Assert.That(html, Does.Match("<p class=\"call-status\">01:2[34]</p>"), "the call timer carries on");
        });
    }

    [Test]
    public async Task APersonWhoIsAway_KeepsTheirDimmedPicture_WithTheirNameOnIt()
    {
        var html = await ReadableAsync("video-peer-away", state =>
        {
            Video(1)(state);
            state.Attempt("Group", new YapGroupCall(Guid.NewGuid(), Guid.NewGuid(), Alex, "Bunsoy", 1, DateTimeOffset.UtcNow.AddMinutes(1),
                [new(Alex, Guid.NewGuid(), true, true, false, false, true, Reconnecting: true), new(Self, Guid.NewGuid(), true, true, false, false, true)]));
        });
        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("class=\"vidtile frozen\""), "their tile stays, dimmed");
            Assert.That(html, Does.Contain("<canvas"), "with the canvas that holds their last picture");
            Assert.That(html, Does.Contain("aria-label=\"Alex Rivera, reconnecting\""));
            Assert.That(html, Does.Match("class=\"tile-away\"[^>]*>.*Alex Rivera · Reconnecting…"));
            Assert.That(LiveRegion(html), Is.EqualTo("Alex Rivera is reconnecting…"));
        });
    }

    [Test]
    public async Task AFrozenPicture_IsDimmedEvenBeforeTheRosterSaysWhy()
    {
        var html = await ReadableAsync("video-peer-frozen", state =>
        {
            Video(1)(state);
            state.Attempt("Tiles", (IReadOnlyList<VideoTile>)[new VideoTile(Guid.NewGuid(), Alex, Frozen: true)]);
        });
        Assert.That(html, Does.Contain("class=\"vidtile frozen\""));
    }

    [Test]
    public async Task AGroup_SaysWhoIsReconnecting()
    {
        var html = await ReadableAsync("group-away", state =>
        {
            Group(videoFor: [Alex], muted: [])(state);
            state.Attempt("Group", new YapGroupCall(Guid.NewGuid(), Guid.NewGuid(), Alex, "Alex", 1, DateTimeOffset.UtcNow.AddMinutes(1),
                [new(Alex, Guid.NewGuid(), true, true, false, false, true), new(Jamie, Guid.NewGuid(), true, true, false, false, false, Reconnecting: true),
                 new(Sam, Guid.NewGuid(), true, true, false, false), new(Self, Guid.NewGuid(), true, true, false, false, true)]));
        });
        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("class=\"vidtile camera-off away\""), "the away person's tile is dimmed");
            Assert.That(html, Does.Match("Jamie Cruz<small>Reconnecting…</small>"));
            Assert.That(Regex.Matches(html, "Reconnecting…").Count, Is.EqualTo(1), "and only theirs");
        });
    }

    [Test]
    public async Task AOneToOneVoiceCall_SaysTheOtherPersonIsReconnecting()
    {
        var html = await ReadableAsync("voice-peer-away", state =>
        {
            Voice(connected: true)(state);
            state.Attempt("Group", new YapGroupCall(Guid.NewGuid(), Guid.NewGuid(), Alex, "Bunsoy", 1, DateTimeOffset.UtcNow.AddMinutes(1),
                [new(Alex, Guid.NewGuid(), true, true, false, false, false, Reconnecting: true), new(Self, Guid.NewGuid(), true, true, false, false)]));
        });
        Assert.That(html, Does.Contain("<p class=\"call-status\">Alex Rivera is reconnecting…</p>"));
    }

    [Test]
    public async Task ACallThatEndedByItself_SaysWhy_AndOffersToCallBack()
    {
        var html = await ReadableAsync("call-ended", Ended());
        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("aria-label=\"Call ended\""), "the dialog says what it is now");
            Assert.That(html, Does.Contain("role=\"alert\">Call ended — connection lost</p>"), "why, announced at once");
            Assert.That(Regex.Matches(html, "<span class=\"lbl\"[^>]*>([^<]+)</span>").Select(x => x.Groups[1].Value), Is.EqualTo(new[] { "Close", "Call back" }));
            Assert.That(html, Does.Contain("<h1 class=\"call-name\">Bunsoy</h1>"), "and who with");
            Assert.That(html, Does.Not.Contain("aria-label=\"End\""), "there is no call left to end");
        });
    }

    [Test]
    public async Task EveryControlOnEveryReconnectScreenHasAName()
    {
        foreach (var (name, setup) in new (string, Action<CallState>)[]
                 {
                     ("voice-reconnecting", Reconnecting(12)),
                     ("video-reconnecting", state => { Video(1)(state); Reconnecting(3)(state); }),
                     ("call-ended", Ended()),
                 })
        {
            var html = await ReadableAsync(name, setup);
            foreach (Match button in Regex.Matches(html, "<button[^>]*>"))
                Assert.That(button.Value, Does.Match("aria-label=\"[^\"]+\""), $"{name}: {button.Value} has no accessible name");
        }
    }

    private static Action<CallState> Reconnecting(int secondsAgo) => state =>
    {
        Voice(connected: true)(state);
        var link = new CallLinkMonitor();
        link.Connected(0);
        link.Lost(1);
        state.Attempt("Link", link);
        state.Attempt("EverConnected", true);
        state.Attempt("ReconnectingSince", (DateTimeOffset?)DateTimeOffset.UtcNow.AddSeconds(-secondsAgo));
        state.Set(nameof(VoiceState.Status), "Reconnecting...");
    };

    private static Action<CallState> Ended() => state =>
    {
        state.Set(nameof(VoiceState.Name), "Bunsoy");
        state.Set(nameof(VoiceState.Ended), new CallEndedNotice("Call ended", "connection lost", Guid.NewGuid(), "Bunsoy", null, false));
        state.Detach();
    };

    /// <summary>The rendered markup as text: entities decoded and CSS-isolation attributes dropped.</summary>
    private static async Task<string> ReadableAsync(string name, Action<CallState> setup) =>
        Regex.Replace(WebUtility.HtmlDecode(await RenderAsync(name, setup)), " b-[a-z0-9]{10}(?=[ >])", "");

    private static string LiveRegion(string html) =>
        Regex.Match(html, "<p class=\"call-sr\" role=\"status\">([^<]*)</p>").Groups[1].Value;

    private static string Button(string html, string label) =>
        Regex.Match(html, $"<button[^>]*aria-label=\"{Regex.Escape(label)}\"[^>]*>").Value;
}
