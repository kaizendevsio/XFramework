using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Bolt.Media.Browser;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Moq;
using NUnit.Framework;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Client.Tests;

/// <summary>
/// Codec choice, send-ladder behaviour, the fragmentation that lets a picture ride the audio-sized
/// SFrame envelope, and the camera's default-off lifecycle.
/// </summary>
public sealed class VideoCallTests
{
    private static VideoCodecLadder Ladder(params VideoCodecSupport[] support)
    {
        var ladder = new VideoCodecLadder();
        foreach (var item in support) ladder.Record(item);
        return ladder;
    }

    private static VideoCodecSupport Full(VideoCodec codec, bool hardware = true, int maxHeight = 1080)
        => new(codec, Encode: true, Decode: true, Hardware: hardware, MaxHeight: maxHeight);

    // ── Codec negotiation ──

    [Test]
    public void MissingWholePicture_IsReportedEvenWhenNoPartialFragmentsArrived()
    {
        var assembler = new VideoFrameAssembler();
        var first = assembler.Add(VideoFrameFragments.Split(new byte[10], 1, 0, true)[0]);
        var afterLoss = assembler.Add(VideoFrameFragments.Split(new byte[10], 3, 2, false)[0]);
        var next = assembler.Add(VideoFrameFragments.Split(new byte[10], 4, 3, true)[0]);
        Assert.Multiple(() =>
        {
            Assert.That(first!.Value.Discontinuity, Is.False);
            Assert.That(afterLoss!.Value.Discontinuity, Is.True);
            Assert.That(next!.Value.Discontinuity, Is.False);
        });
    }

    [Test]
    public void NegotiatedCodecKeepsItsSoftwareAndDeviceCeiling()
    {
        var ladder = new VideoCodecLadder();
        ladder.Record(new(VideoCodec.Av1, true, true, false, 720));
        ladder.Record(new(VideoCodec.H264, true, true, true, 540));
        Assert.Multiple(() =>
        {
            Assert.That(ladder.EncodingCeiling(VideoCodec.Av1), Is.EqualTo(360));
            Assert.That(ladder.EncodingCeiling(VideoCodec.H264), Is.EqualTo(540));
            Assert.That(ladder.EncodingCeiling(VideoCodec.None), Is.Zero);
        });
    }

    [Test]
    public void HardwareAv1_IsPreferredWhenEveryPeerCanDecodeIt()
    {
        var ladder = Ladder(Full(VideoCodec.Av1), Full(VideoCodec.Vp9), Full(VideoCodec.H264));
        var chosen = ladder.Negotiate([[VideoCodec.Av1, VideoCodec.H264]], 1080);
        Assert.That(chosen, Is.EqualTo(VideoCodec.Av1));
    }

    // Software AV1 at 1080p on a phone is a slideshow. The ladder must fall through to a codec
    // this device can actually keep up with rather than promise compression it cannot deliver.
    [Test]
    public void SoftwareOnlyAv1_IsRefusedAboveItsCeilingAndFallsThroughToVp9()
    {
        var ladder = Ladder(Full(VideoCodec.Av1, hardware: false), Full(VideoCodec.Vp9), Full(VideoCodec.H264));
        VideoCodec[] peer = [VideoCodec.Av1, VideoCodec.Vp9, VideoCodec.H264];
        Assert.Multiple(() =>
        {
            Assert.That(ladder.Negotiate([peer], 1080), Is.EqualTo(VideoCodec.Vp9));
            Assert.That(ladder.Negotiate([peer], 360), Is.EqualTo(VideoCodec.Av1),
                "software AV1 is worth its CPU at the small sizes, which is where low bandwidth lands");
        });
    }

    [Test]
    public void APeerThatCannotDecodeTheBestCodec_DropsTheWholeCallToWhatItShares()
    {
        var ladder = Ladder(Full(VideoCodec.Av1), Full(VideoCodec.Vp9), Full(VideoCodec.H264));
        Assert.Multiple(() =>
        {
            Assert.That(ladder.Negotiate([[VideoCodec.Av1, VideoCodec.Vp9, VideoCodec.H264], [VideoCodec.H264]], 720),
                Is.EqualTo(VideoCodec.H264), "one old phone sets the codec for the whole call");
            Assert.That(ladder.Negotiate([[VideoCodec.Vp9, VideoCodec.H264], [VideoCodec.Vp9, VideoCodec.H264]], 720), Is.EqualTo(VideoCodec.Vp9));
            Assert.That(ladder.Negotiate([[VideoCodec.Av1], [VideoCodec.Vp9]], 720), Is.EqualTo(VideoCodec.None),
                "peers with nothing in common get no video rather than a picture one of them cannot read");
        });
    }

    [Test]
    public void APeerThatAdvertisedNothing_IsTreatedAsH264Only()
    {
        var ladder = Ladder(Full(VideoCodec.Av1), Full(VideoCodec.H264));
        Assert.That(ladder.Negotiate([[]], 720), Is.EqualTo(VideoCodec.H264));
    }

    [Test]
    public void ADeviceWithNoVideoEncoder_NegotiatesNothingRatherThanGuessing()
    {
        var ladder = Ladder(new VideoCodecSupport(VideoCodec.H264, Encode: false, Decode: true, Hardware: false, MaxHeight: 0));
        Assert.That(ladder.Negotiate([[VideoCodec.H264]], 360), Is.EqualTo(VideoCodec.None));
    }

    [Test]
    public void EncoderHeightLimits_ExcludeACodecThatCannotReachTheAskedSize()
    {
        var ladder = Ladder(Full(VideoCodec.Av1, maxHeight: 540), Full(VideoCodec.H264));
        Assert.Multiple(() =>
        {
            Assert.That(ladder.Negotiate([[VideoCodec.Av1, VideoCodec.H264]], 1080), Is.EqualTo(VideoCodec.H264));
            Assert.That(ladder.Negotiate([[VideoCodec.Av1, VideoCodec.H264]], 540), Is.EqualTo(VideoCodec.Av1));
        });
    }

    [Test]
    public void Advertisement_RoundTripsAndIgnoresAnythingUnrecognised()
    {
        var ladder = Ladder(Full(VideoCodec.Av1), new VideoCodecSupport(VideoCodec.Vp9, false, true, false, 0), Full(VideoCodec.H264));
        var advertised = VideoCodecLadder.Advertise(ladder.Decodable);
        Assert.Multiple(() =>
        {
            Assert.That(advertised, Is.EqualTo("av1,vp9,h264"), "decode support is advertised even where encode is not");
            Assert.That(VideoCodecLadder.ReadAdvertisement(advertised), Is.EqualTo(new[] { VideoCodec.Av1, VideoCodec.Vp9, VideoCodec.H264 }));
            Assert.That(VideoCodecLadder.ReadAdvertisement("h265,,av1,nonsense"), Is.EqualTo(new[] { VideoCodec.Av1 }));
            Assert.That(VideoCodecLadder.ReadAdvertisement(null), Is.Empty);
        });
    }

    // ── Adaptation ──

    private static VideoConditions Good(int kbps) => new(kbps, 30, 0, 0);

    [Test]
    public void OneBadWindowIsIgnored_ButTwoInARowDropATier()
    {
        var adaptation = new VideoAdaptation(3);
        Assert.Multiple(() =>
        {
            Assert.That(adaptation.Observe(new(600, 30, 0, 0)), Is.Null, "a single dip is not a trend");
            Assert.That(adaptation.Observe(new(600, 30, 0, 0))!.Value.Height, Is.EqualTo(540));
        });
    }

    // A thermally throttled phone still reports plenty of bandwidth; the encode backlog is the
    // only honest signal that this device cannot hold the tier it is on.
    [Test]
    public void AnEncodeBacklogDropsTheTierEvenWithBandwidthToSpare()
    {
        var adaptation = new VideoAdaptation(5);
        adaptation.Observe(new(20_000, 30, 5, 0));
        var tier = adaptation.Observe(new(20_000, 30, 5, 0));
        Assert.That(tier!.Value.Height, Is.EqualTo(900));
    }

    [Test]
    public void AFrameRateFarBelowTheTier_CountsAsStrainEvenWhenNothingIsQueued()
    {
        var adaptation = new VideoAdaptation(3);
        adaptation.Observe(new(20_000, 12, 0, 0));
        Assert.That(adaptation.Observe(new(20_000, 12, 0, 0))!.Value.Height, Is.EqualTo(540));
    }

    [Test]
    public void ClimbingBackUpTakesTwelveCleanWindowsAndOnlyOneRungAtATime()
    {
        var adaptation = new VideoAdaptation(2);
        for (var i = 0; i < VideoAdaptation.UpAfter - 1; i++)
            Assert.That(adaptation.Observe(Good(20_000)), Is.Null, $"window {i} must not be enough on its own");
        var tier = adaptation.Observe(Good(20_000));
        Assert.Multiple(() =>
        {
            Assert.That(tier!.Value.Height, Is.EqualTo(720));
            Assert.That(adaptation.Observe(Good(20_000)), Is.Null, "the next rung needs its own twelve windows");
        });
    }

    [Test]
    public void TheCeilingClampsTheLadderImmediatelyAndBlocksFurtherClimbing()
    {
        var adaptation = new VideoAdaptation(5);
        Assert.That(adaptation.SetCeiling(540), Is.True);
        Assert.That(adaptation.Current!.Value.Height, Is.EqualTo(540));
        for (var i = 0; i < VideoAdaptation.UpAfter * 2; i++) adaptation.Observe(Good(20_000));
        Assert.That(adaptation.Current!.Value.Height, Is.EqualTo(540), "a battery or participant cap is not negotiable");
    }

    // Below this the picture is worthless and the voice needs the room, so video stands down and
    // comes back only when the link can carry the smallest rung with headroom.
    [Test]
    public void ALinkTooSmallForAnyPicture_SuspendsVideoAndResumesWhenItRecovers()
    {
        var adaptation = new VideoAdaptation(3);
        for (var i = 0; i < VideoAdaptation.SuspendAfter; i++) adaptation.Observe(new(60, 8, 0, 0));
        Assert.That(adaptation.Suspended, Is.True);
        Assert.That(adaptation.Current, Is.Null);
        Assert.That(adaptation.Observe(new(200, 0, 0, 0)), Is.Null, "one good reading is not enough headroom");
        var resumed = adaptation.Observe(new(600, 0, 0, 0));
        Assert.Multiple(() =>
        {
            Assert.That(adaptation.Suspended, Is.False);
            Assert.That(resumed!.Value.Height, Is.EqualTo(240), "it comes back at the bottom and climbs from there");
        });
    }

    // Nothing is being sent while video is suspended, so the receiver stops reporting a bitrate.
    // Without a blind retry the picture would never come back on a link that has recovered.
    [Test]
    public void ASuspendedPicture_IsRetriedEvenWhenNoFeedbackArrives()
    {
        var adaptation = new VideoAdaptation(2);
        for (var i = 0; i < VideoAdaptation.SuspendAfter; i++) adaptation.Observe(new(50, 5, 0, 0));
        Assert.That(adaptation.Suspended, Is.True);
        VideoTier? resumed = null;
        for (var i = 0; i < VideoAdaptation.ResumeProbeAfter && resumed is null; i++) resumed = adaptation.Observe(new(0, 0, 0, 0));
        Assert.Multiple(() =>
        {
            Assert.That(resumed, Is.Not.Null, "a silent link still gets retried after the cool-down");
            Assert.That(adaptation.Suspended, Is.False);
        });
        for (var i = 0; i < VideoAdaptation.SuspendAfter; i++) adaptation.Observe(new(50, 5, 0, 0));
        Assert.That(adaptation.Suspended, Is.True, "a link that is still bad stands the picture down again");
    }

    [Test]
    public void GroupSize_LowersTheCeilingBecauseEveryExtraSenderIsAnotherDecode()
    {
        Assert.Multiple(() =>
        {
            Assert.That(VideoAdaptation.HeightCapForParticipants(2), Is.EqualTo(2160));
            Assert.That(VideoAdaptation.HeightCapForParticipants(3), Is.EqualTo(720));
            Assert.That(VideoAdaptation.HeightCapForParticipants(4), Is.EqualTo(540));
            Assert.That(VideoAdaptation.MaxVideoParticipants, Is.EqualTo(4));
        });
    }

    [Test]
    public void Requested4K60_DropsFrameRateBeforeResolutionUnderPressure()
    {
        var adaptation = new VideoAdaptation(VideoAdaptation.IndexForHeight(2160), 60);
        Assert.That(adaptation.Current, Is.EqualTo(new VideoTier(3840, 2160, 60, 21000)));
        adaptation.Observe(new(30000, 25, 3, 0));
        Assert.That(adaptation.Observe(new(30000, 25, 3, 0)), Is.EqualTo(new VideoTier(3840, 2160, 30, 14000)));
        adaptation.Observe(new(30000, 25, 3, 0));
        Assert.That(adaptation.Observe(new(30000, 25, 3, 0))!.Value.Height, Is.EqualTo(1440));
    }

    [Test]
    public void DefaultStartsAt1080p30_AndPreferenceCapsUpscaling()
    {
        var adaptation = new VideoAdaptation(new MediaServiceOptions().VideoStartTier);
        adaptation.SetCeiling(1080);
        Assert.That(adaptation.Current, Is.EqualTo(new VideoTier(1920, 1080, 30, 3800)));
        for (var i = 0; i < 30; i++) adaptation.Observe(Good(50000));
        Assert.That(adaptation.Current!.Value.Height, Is.EqualTo(1080));
    }

    // ── Fragmentation: every picture rides the same 4 KB authenticated envelope as an Opus packet ──

    [Test]
    public void AKeyframeFarLargerThanTheSFrameLimit_SurvivesASplitAndReassembly()
    {
        var picture = RandomNumberGenerator.GetBytes(120_000);
        var fragments = VideoFrameFragments.Split(picture, frameId: 7, timestampMicroseconds: 123456, isKeyframe: true);
        Assert.That(fragments, Has.Count.EqualTo(VideoFrameFragments.FragmentCount(picture.Length)));
        Assert.That(fragments.All(x => x.Length <= VideoFrameFragments.MaxPlaintext), Is.True,
            "no fragment may exceed what the SFrame session will encrypt");

        var assembler = new VideoFrameAssembler();
        VideoFramePayload? built = null;
        foreach (var fragment in fragments) built ??= assembler.Add(fragment);
        Assert.Multiple(() =>
        {
            Assert.That(built, Is.Not.Null);
            Assert.That(built!.Value.Data, Is.EqualTo(picture));
            Assert.That(built.Value.IsKeyframe, Is.True);
            Assert.That(built.Value.TimestampMicroseconds, Is.EqualTo(123456u));
        });
    }

    [Test]
    public void FragmentsThatArriveOutOfOrder_StillRebuildTheSamePicture()
    {
        var picture = RandomNumberGenerator.GetBytes(20_000);
        var fragments = VideoFrameFragments.Split(picture, 1, 500, false);
        var assembler = new VideoFrameAssembler();
        VideoFramePayload? built = null;
        foreach (var fragment in fragments.AsEnumerable().Reverse()) built ??= assembler.Add(fragment);
        Assert.That(built!.Value.Data, Is.EqualTo(picture));
    }

    [Test]
    public void APictureBeyondTheReassemblyBound_IsDroppedRatherThanPartiallySent()
    {
        var huge = new byte[VideoFrameFragments.MaxPayload * (VideoFrameFragments.MaxFragments + 1)];
        Assert.That(VideoFrameFragments.Split(huge, 1, 0, true), Is.Empty);
    }

    [Test]
    public void ReplayedAndTruncatedFragments_AreRefused()
    {
        var picture = RandomNumberGenerator.GetBytes(9_000);
        var fragments = VideoFrameFragments.Split(picture, 42, 0, true);
        var assembler = new VideoFrameAssembler();
        foreach (var fragment in fragments) assembler.Add(fragment);

        Assert.Multiple(() =>
        {
            Assert.That(assembler.Add(fragments[0]), Is.Null, "a completed picture cannot be rebuilt from replayed fragments");
            // Only the last fragment may be short; a shortened middle one is a forged or truncated split.
            var tampered = fragments[0][..(VideoFrameFragments.HeaderSize + 10)];
            Assert.That(new VideoFrameAssembler().Add(tampered), Is.Null);
            Assert.That(new VideoFrameAssembler().Add(new byte[VideoFrameFragments.HeaderSize]), Is.Null);
        });
    }

    [Test]
    public void AbandonedPictures_AreBoundedAndCounted()
    {
        var assembler = new VideoFrameAssembler();
        for (uint frame = 1; frame <= 6; frame++)
            assembler.Add(VideoFrameFragments.Split(new byte[9_000], frame, 0, false)[0]);
        Assert.That(assembler.Incomplete, Is.EqualTo(3), "at most three pictures are held while their fragments arrive");
    }

    [Test]
    public void SenderIdentifiers_MapBackToTheParticipantTheyBelongTo()
    {
        var call = Guid.NewGuid();
        var credential = Guid.NewGuid();
        Assert.Multiple(() =>
        {
            Assert.That(VoiceState.CredentialOf($"yap-media-{call:N}-{credential:N}"), Is.EqualTo(credential));
            Assert.That(VoiceState.CredentialOf("nonsense"), Is.EqualTo(Guid.Empty));
        });
    }

    // ── What the user is told when video will not start ──

    // "This device cannot encode video" reads as a hardware limit. On iOS Safari the real cause was
    // a missing browser API, and nothing in that wording would make anyone try another browser.
    [Test]
    public void TheVideoNotice_NamesTheBrowserWhenTheBrowserIsTheProblem()
    {
        const string browser = "This browser cannot send video in calls. Try the latest Safari, Chrome, Edge or Firefox.";
        Assert.Multiple(() =>
        {
            Assert.That(VoiceState.VideoBlockedNotice(browser, anyLocalEncoder: false), Is.EqualTo(browser),
                "a browser-level refusal survives negotiation rather than being overwritten by the codec outcome");
            Assert.That(VoiceState.VideoBlockedNotice(null, anyLocalEncoder: true),
                Is.EqualTo("No video format works for everyone on this call."));
            Assert.That(VoiceState.VideoBlockedNotice(null, anyLocalEncoder: false),
                Is.EqualTo("This device has no video encoder for calls."));
            Assert.That(VoiceState.VideoBlockedNotice(browser, anyLocalEncoder: false),
                Does.Not.Contain("cannot encode"), "the old wording blamed hardware for a browser problem");
        });
    }

    // ── Camera lifecycle in VoiceState ──

    [Test]
    public async Task VideoIsOfferedOnlyWhenTheServerSaysSo()
    {
        await using var audioOnly = new VideoFixture(video: false);
        await audioOnly.Voice.InitializeAsync();
        Assert.Multiple(() =>
        {
            Assert.That(audioOnly.Voice.Enabled, Is.True);
            Assert.That(audioOnly.Voice.VideoAvailable, Is.False);
        });

        await using var withVideo = new VideoFixture(video: true);
        await withVideo.Voice.InitializeAsync();
        Assert.That(withVideo.Voice.VideoAvailable, Is.True);
    }

    // The camera is a privacy surface: nothing in the answer path may open it.
    [Test]
    public async Task AnsweringACall_NeverTurnsTheCameraOn()
    {
        await using var fixture = new VideoFixture(video: true);
        await fixture.Voice.InitializeAsync();
        await fixture.DeliverAsync(fixture.GroupEvent(fixture.Invite(), callerVideo: true));
        Assert.That(fixture.Voice.IncomingHasVideo, Is.True, "the incoming screen should still offer to answer with video");
        await fixture.Voice.AcceptAsync();
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Voice.CameraOn, Is.False);
            Assert.That(fixture.Voice.RemoteVideo, Is.Empty);
            Assert.That(fixture.Voice.AnyVideo, Is.False);
        });
    }

    [Test]
    public async Task TogglingTheCameraBeforeTheCallConnects_DoesNothing()
    {
        await using var fixture = new VideoFixture(video: true);
        await fixture.Voice.InitializeAsync();
        await fixture.DeliverAsync(fixture.GroupEvent(fixture.Invite()));
        await fixture.Voice.ToggleCameraAsync();
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Voice.CameraOn, Is.False);
            Assert.That(fixture.Posts, Has.None.Contains("/video"), "no camera claim is made before the keys are live");
        });
    }

    [Test]
    public async Task EndingACall_ClearsEveryTraceOfVideo()
    {
        await using var fixture = new VideoFixture(video: true);
        await fixture.Voice.InitializeAsync();
        await fixture.DeliverAsync(fixture.GroupEvent(fixture.Invite()));
        await fixture.Voice.EndAsync().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Voice.CameraOn, Is.False);
            Assert.That(fixture.Voice.VideoQuality, Is.Null);
            Assert.That(fixture.Voice.VideoNotice, Is.Null);
            Assert.That(fixture.Voice.RemoteVideo, Is.Empty);
        });
    }

    // Audio-only behaviour must be untouched by any of the above.
    [Test]
    public void RingingIsStillDerivedOnlyFromCallState()
    {
        Assert.Multiple(() =>
        {
            Assert.That(VoiceState.RingModeFor(true, true, "Incoming encrypted voice call"), Is.EqualTo("ringtone"));
            Assert.That(VoiceState.RingModeFor(true, false, VoiceState.RingingStatus), Is.EqualTo("ringback"));
            Assert.That(VoiceState.RingModeFor(true, false, "Connected"), Is.Empty);
        });
    }

    private sealed class VideoFixture : IAsyncDisposable
    {
        private readonly ServiceProvider provider;
        private readonly HttpClient http;
        private readonly UserSession user = new(Guid.NewGuid(), Guid.NewGuid(), "Recipient");
        public ChatApi Api { get; }
        public ChatState Chat { get; }
        public VoiceState Voice { get; }
        public List<string> Posts { get; } = [];

        public VideoFixture(bool video)
        {
            var js = Mock.Of<IJSRuntime>();
            http = new HttpClient(new VideoHandler(request =>
            {
                lock (Posts) Posts.Add(request.RequestUri!.AbsolutePath);
                return request.RequestUri!.AbsolutePath.EndsWith("/config")
                    ? new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = JsonContent.Create(new { enabled = true, groupCalls = true, securityMode = "EndToEndEncrypted", video }) }
                    : new HttpResponseMessage(HttpStatusCode.NoContent);
            }))
            { BaseAddress = new("https://yap.test/") };
            Api = new ChatApi(http) { Account = OfflineStore.Scope(user) };
            Chat = new ChatState(null!, Api, js);
            typeof(ChatState).GetProperty(nameof(ChatState.User))!.SetValue(Chat, user);
            Chat.Encryption.Status.Approved = true;
            Chat.Encryption.Status.DeviceId = Guid.NewGuid();
            var services = new ServiceCollection();
            services.AddLogging(); services.AddSingleton(js); services.AddBoltMediaBrowser();
            provider = services.BuildServiceProvider();
            Voice = new VoiceState(Chat, Api, provider.GetRequiredService<IServiceScopeFactory>(), new VideoNavigation(), NullLoggerFactory.Instance, js);
        }

        public YapCallInvite Invite() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Caller", user.CredentialId, DateTimeOffset.UtcNow.AddMinutes(1));
        public YapCallEvent GroupEvent(YapCallInvite invite, bool callerVideo = false) => new("group-incoming", invite,
            Group: new(invite.Id, invite.ThreadId, invite.CallerId, invite.CallerName, 1, invite.ExpiresAt,
            [new(invite.CallerId, Guid.NewGuid(), true, false, false, false, callerVideo),
             new(user.CredentialId, Guid.Empty, false, false, false, false)]));
        public Task DeliverAsync(YapCallEvent value) => Chat.VoiceEvent(JsonSerializer.Serialize(value));
        public async ValueTask DisposeAsync()
        { await Voice.DisposeAsync(); await Chat.DisposeAsync(); await provider.DisposeAsync(); http.Dispose(); }
    }

    private sealed class VideoHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }

    private sealed class VideoNavigation : NavigationManager
    { public VideoNavigation() => Initialize("https://yap.test/", "https://yap.test/"); }
}
