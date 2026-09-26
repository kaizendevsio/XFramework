using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Bolt.Media;
using Bolt.Media.Browser;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using NUnit.Framework;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Client.Tests;

/// <summary>
/// The reconnect state machine inside VoiceState: a lost transport becomes "Reconnecting" instead of
/// a hang-up, resume tickets are asked for this seat and device, a network hint retries at once, a
/// refusal or the end of the grace period ends the call with a clear reason and a way to call back,
/// and nothing the user does while reconnecting (mute, hang up) is lost or ends the call by itself.
/// The timing rules themselves are tested on a fake clock in CallLinkMonitorTests.
/// </summary>
public sealed partial class VoiceStateTests
{
    [Test]
    public async Task LostTransport_ShowsReconnecting_KeepsTheCall_AndAsksForThisDevicesSeat()
    {
        await using var f = new ResumeFixture(resume: HttpStatusCode.ServiceUnavailable);
        var attempt = f.Connected();

        await f.LoseTransportAsync(attempt);

        var first = await f.NextResumeAsync();
        Assert.Multiple(() =>
        {
            Assert.That(f.Voice.IsCalling, Is.True, "a lost socket is not a hang-up");
            Assert.That(f.Voice.Reconnecting, Is.True);
            Assert.That(f.Voice.Status, Is.EqualTo("Reconnecting..."));
            Assert.That(f.Voice.ReconnectDeadline, Is.EqualTo(f.Voice.ReconnectingSince!.Value.AddSeconds(45)), "the phone gives up exactly when the seat does");
            Assert.That(first.DeviceId, Is.EqualTo(f.Device), "the resume ticket is for this device's seat");
            Assert.That(f.Requests, Does.Not.Contain("POST /api/chat/calls/groups/" + f.Call + "/leave"), "and the server is not told the call ended");
        });

        await f.Voice.EndAsync();
        Assert.That(() => f.Requests, Does.Contain("POST /api/chat/calls/groups/" + f.Call + "/leave").After(3000, 20), "hanging up while reconnecting leaves the call");
        var tickets = f.ResumeCount;
        await Task.Delay(1200);
        Assert.That(f.ResumeCount, Is.EqualTo(tickets), "and stops the reconnect loop");
    }

    [TestCase(HttpStatusCode.NotFound)]
    [TestCase(HttpStatusCode.Forbidden)]
    public async Task RefusedResume_EndsTheCall_SaysConnectionLost_AndOffersCallBack(HttpStatusCode refusal)
    {
        await using var f = new ResumeFixture(resume: refusal);
        var attempt = f.Connected();
        f.Voice.Minimized = true;

        await f.LoseTransportAsync(attempt);

        Assert.That(() => f.Voice.Ended, Is.Not.Null.After(5000, 20));
        Assert.Multiple(() =>
        {
            Assert.That(f.Voice.IsCalling, Is.False);
            Assert.That(f.Voice.Ended!.Title, Is.EqualTo("Call ended"));
            Assert.That(f.Voice.Ended.Detail, Is.EqualTo("connection lost"));
            Assert.That(f.Voice.Ended.ThreadId, Is.EqualTo(f.Thread), "call back goes to the same conversation");
            Assert.That(f.Voice.Minimized, Is.False, "a call that ended by itself is shown, not left in the pill");
            Assert.That(f.Voice.Error, Is.Null, "an ended call is not an error sheet");
            Assert.That(f.ResumeCount, Is.EqualTo(1), "a refused seat is not asked for again");
        });
    }

    [Test]
    public async Task GraceRunsOut_EndsTheCall_WithConnectionLost()
    {
        await using var f = new ResumeFixture(resume: HttpStatusCode.ServiceUnavailable);
        var attempt = f.Connected(new CallLinkOptions { GraceMs = 1_500 });
        await f.LoseTransportAsync(attempt);
        Assert.That(() => f.Voice.Ended?.Detail, Is.EqualTo("connection lost").After(6000, 20));
        Assert.That(f.ResumeCount, Is.GreaterThan(1), "it kept trying until the grace ran out");
    }

    [Test]
    public async Task NetworkHint_WhileReconnecting_RetriesAtOnce()
    {
        await using var f = new ResumeFixture(resume: HttpStatusCode.ServiceUnavailable);
        // The second attempt would otherwise wait a minute.
        var attempt = f.Connected(new CallLinkOptions { BackoffMs = [0, 60_000] });
        await f.LoseTransportAsync(attempt);
        await f.NextResumeAsync();
        await Task.Delay(300);
        Assert.That(f.ResumeCount, Is.EqualTo(1));

        await f.Voice.OnCallNetworkChanged("online");
        Assert.That(() => f.ResumeCount, Is.EqualTo(2).After(2000, 20), "the phone found a network: try now");
    }

    [Test]
    public async Task SilentLink_IsNoticed_PoorFirst_ThenReconnecting()
    {
        await using var f = new ResumeFixture(resume: HttpStatusCode.ServiceUnavailable);
        var attempt = f.Connected(new CallLinkOptions { HeartbeatIntervalMs = 300, DegradedSilenceMs = 600, LostSilenceMs = 1_800 });
        var link = (CallLinkMonitor)Field(attempt, "Link");
        link.Echo(Environment.TickCount64 - 30, Environment.TickCount64); // the relay answered once, then went quiet
        var seen = new ConcurrentQueue<CallLinkState>();
        f.Voice.Changed += () => seen.Enqueue(f.Voice.Link);
        _ = (Task)typeof(VoiceState).GetMethod("LivenessAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(f.Voice, [attempt])!;

        Assert.That(() => f.Voice.Reconnecting, Is.True.After(6000, 20));
        await f.NextResumeAsync();
        Assert.That(seen.TakeWhile(x => x != CallLinkState.Reconnecting), Does.Contain(CallLinkState.Degraded), "Poor connection came first");
    }

    [Test]
    public async Task MuteWhileReconnecting_TakesEffectLocally_AndDoesNotEndTheCall()
    {
        await using var f = new ResumeFixture(resume: HttpStatusCode.ServiceUnavailable);
        var attempt = f.Connected();
        await f.LoseTransportAsync(attempt);
        await f.NextResumeAsync();

        await f.Voice.ToggleMuteAsync();
        Assert.Multiple(() =>
        {
            Assert.That(f.Voice.Muted, Is.True);
            Assert.That(f.Voice.IsCalling, Is.True);
            Assert.That(f.Requests, Does.Not.Contain("POST /api/chat/calls/groups/" + f.Call + "/mute"), "the server hears about it after the resume");
            Assert.That(Field(attempt, "MuteDirty"), Is.True);
        });
    }

    [Test]
    public async Task VanishedPicture_StaysFrozenWhileItsSenderReconnects_AndGoesWhenTheyLeave()
    {
        await using var f = new ResumeFixture(resume: HttpStatusCode.ServiceUnavailable);
        var attempt = f.Connected();
        var tile = new VideoTile(Guid.NewGuid(), f.Peer);
        Set(attempt, "Tiles", (IReadOnlyList<VideoTile>)[tile]);
        var apply = typeof(VoiceState).GetMethod("ApplyRemoteVideo", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var refresh = typeof(VoiceState).GetMethod("RefreshFrozenTiles", BindingFlags.Instance | BindingFlags.NonPublic)!;

        // The relay ended the stream; the roster says the sender is only reconnecting.
        Set(attempt, "Group", f.Roster(peerAway: true));
        apply.Invoke(f.Voice, [attempt]);
        Assert.That(f.Voice.RemoteVideo, Is.EqualTo(new[] { tile with { Frozen = true } }), "their last picture stays, frozen");

        Set(attempt, "Group", f.Roster(peerLeft: true));
        refresh.Invoke(f.Voice, [attempt]);
        Assert.That(f.Voice.RemoteVideo, Is.Empty, "and goes once they have left");
    }

    [Test]
    public async Task VanishedPicture_WithoutRosterNews_IsDroppedAfterAShortWhile()
    {
        await using var f = new ResumeFixture(resume: HttpStatusCode.ServiceUnavailable);
        var attempt = f.Connected();
        var tile = new VideoTile(Guid.NewGuid(), f.Peer);
        Set(attempt, "Tiles", (IReadOnlyList<VideoTile>)[tile]);
        typeof(VoiceState).GetMethod("ApplyRemoteVideo", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(f.Voice, [attempt]);
        Assert.That(f.Voice.RemoteVideo.Single().Frozen, Is.True, "the stream ending can arrive before the roster says why");

        var frozen = (System.Collections.IDictionary)Field(attempt, "Frozen");
        frozen[f.Peer] = (tile with { Frozen = true }, Environment.TickCount64 - 6_000);
        typeof(VoiceState).GetMethod("RefreshFrozenTiles", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(f.Voice, [attempt]);
        Assert.That(f.Voice.RemoteVideo, Is.Empty, "a camera that simply stopped does not stay frozen forever");
    }

    [Test]
    public async Task CallBack_FromTheEndedScreen_CallsTheSameConversationAgain()
    {
        await using var f = new ResumeFixture(resume: HttpStatusCode.NotFound);
        var attempt = f.Connected();
        await f.LoseTransportAsync(attempt);
        Assert.That(() => f.Voice.Ended, Is.Not.Null.After(5000, 20));

        await f.Voice.CallBackAsync();
        Assert.Multiple(() =>
        {
            Assert.That(f.Requests, Does.Contain($"GET /api/chat/conversations/{f.Thread}"));
            Assert.That(f.Voice.Ended, Is.Null, "the ended screen gave way to the new call");
        });
        await f.Voice.EndAsync();
    }

    private static object Field(object target, string name) =>
        (target.GetType().GetField(name)?.GetValue(target) ?? target.GetType().GetProperty(name)!.GetValue(target))!;
    private static void Set(object target, string name, object? value) => target.GetType().GetField(name)!.SetValue(target, value);

    /// <summary>A connected one-to-one call whose server answers every resume with <c>resume</c>.</summary>
    private sealed class ResumeFixture : IAsyncDisposable
    {
        private readonly Fixture fixture;
        private readonly ServiceProvider media;
        private readonly SemaphoreSlim resumed = new(0);
        private readonly ConcurrentQueue<YapGroupResume> bodies = new();
        public Guid Call { get; } = Guid.NewGuid();
        public Guid Thread { get; } = Guid.NewGuid();
        public Guid Peer { get; } = Guid.NewGuid();
        public Guid Device => fixture.Chat.Encryption.LocalDeviceId!.Value;
        public ConcurrentQueue<string> Log { get; } = new();
        public string[] Requests => [.. Log];
        public int ResumeCount => bodies.Count;
        public VoiceState Voice => fixture.Voice;

        public ResumeFixture(HttpStatusCode resume)
        {
            fixture = new Fixture(async (request, ct) =>
            {
                var path = request.RequestUri!.AbsolutePath;
                Log.Enqueue($"{request.Method} {path}");
                if (path.EndsWith("/resume"))
                {
                    bodies.Enqueue((await request.Content!.ReadFromJsonAsync<YapGroupResume>(ct))!);
                    resumed.Release();
                    return new HttpResponseMessage(resume);
                }
                if (path.EndsWith("/config")) return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { enabled = true, groupCalls = true, securityMode = "EndToEndEncrypted" }) };
                if (path.StartsWith("/api/chat/conversations/")) return new(HttpStatusCode.OK)
                { Content = JsonContent.Create(new Conversation { Id = Thread, Name = "Alex", People = [new(Peer, "Alex", "alex")] }) };
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }, encrypted: true);
            var services = new ServiceCollection();
            services.AddLogging(); services.AddSingleton(Mock.Of<IJSRuntime>());
            services.AddBoltMediaBrowser(options => options.SecurityMode = MediaSecurityMode.AuthenticatedSFrame);
            media = services.BuildServiceProvider();
        }

        public YapGroupCall Roster(bool peerAway = false, bool peerLeft = false) => new(Call, Thread, Peer, "Alex", 1, DateTimeOffset.UtcNow.AddMinutes(1),
            [new(Peer, Guid.NewGuid(), true, true, peerLeft, false, true, peerAway), new(fixture.Chat.User!.CredentialId, Device, true, true, false, false)]);

        /// <summary>Put VoiceState in the middle of an answered call, as ConnectEncryptedGroupAsync leaves it.</summary>
        public object Connected(CallLinkOptions? options = null)
        {
            var type = typeof(VoiceState).GetNestedType("Attempt", BindingFlags.NonPublic)!;
            var attempt = Activator.CreateInstance(type, BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, [fixture.Chat.Scope], null)!;
            var link = new CallLinkMonitor(options ?? new CallLinkOptions());
            link.Connected(Environment.TickCount64);
            Set(attempt, "Group", Roster());
            Set(attempt, "Invite", new YapCallInvite(Call, Thread, Peer, "Alex", fixture.Chat.User!.CredentialId, DateTimeOffset.UtcNow.AddMinutes(1)));
            Set(attempt, "Media", media.GetRequiredService<BoltMediaService>());
            Set(attempt, "Link", link);
            Set(attempt, "EverConnected", true);
            Set(attempt, "TransportReady", true);
            typeof(VoiceState).GetField("active", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Voice, attempt);
            typeof(VoiceState).GetProperty(nameof(VoiceState.ConnectedAt))!.SetValue(Voice, (DateTimeOffset?)DateTimeOffset.UtcNow.AddMinutes(-2));
            typeof(VoiceState).GetProperty(nameof(VoiceState.Status))!.SetValue(Voice, "Connected");
            typeof(VoiceState).GetProperty(nameof(VoiceState.Name))!.SetValue(Voice, "Alex");
            return attempt;
        }

        /// <summary>What the BoltClient's Disconnected event sets off for the connected transport.</summary>
        public Task LoseTransportAsync(object attempt) =>
            (Task)typeof(VoiceState).GetMethod("BeginReconnectAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Voice, [attempt, false])!;

        public async Task<YapGroupResume> NextResumeAsync()
        {
            Assert.That(await resumed.WaitAsync(TimeSpan.FromSeconds(5)), Is.True, "a resume ticket was requested");
            return bodies.Last();
        }

        public async ValueTask DisposeAsync()
        {
            await fixture.DisposeAsync();
            await media.DisposeAsync();
        }
    }
}
