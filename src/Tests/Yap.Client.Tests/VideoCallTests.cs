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

    // ── Temporal layers ──

    [Test]
    public void ALayeredStream_ToleratesTheGapsItsDropPolicyMakes()
    {
        // L1T3 (0,2,1,2): the relay shed pictures 2 and 4 (top layer) for this receiver.
        var assembler = new VideoFrameAssembler();
        var got = new List<VideoFramePayload>();
        foreach (var (frame, layer, key) in new[] { (1u, 0, true), (3u, 1, false), (5u, 0, false), (6u, 2, false) })
            got.Add(assembler.Add(VideoFrameFragments.Split(new byte[10], frame, frame * 1000, key, layer)[0])!.Value);
        Assert.Multiple(() =>
        {
            Assert.That(assembler.Layered, Is.True);
            Assert.That(got.Select(x => x.Discontinuity), Is.All.False, "a policy gap is not a break: no decoder reset, no keyframe");
            Assert.That(got.Select(x => x.Layer), Is.EqualTo(new[] { 0, 1, 0, 2 }), "the layer comes from the authenticated header");
        });
    }

    [Test]
    public void ALayeredStream_StillBreaksOnFragmentsThisDeviceDroppedItself()
    {
        var assembler = new VideoFrameAssembler();
        assembler.Add(VideoFrameFragments.Split(new byte[10], 1, 0, true)[0]);
        assembler.Add(VideoFrameFragments.Split(new byte[10], 2, 1, false, 2)[0]);
        assembler.MarkLocalLoss();
        var afterLoss = assembler.Add(VideoFrameFragments.Split(new byte[10], 4, 3, false, 0)[0]);
        var next = assembler.Add(VideoFrameFragments.Split(new byte[10], 6, 5, false, 0)[0]);
        Assert.That(afterLoss!.Value.Discontinuity, Is.True, "no drop policy covered what this receiver lost");
        Assert.That(next!.Value.Discontinuity, Is.False, "one break, then a policy gap again");
    }

    [Test]
    public void TheLayerBitsAreInvisibleToAReceiverThatPredatesThem()
    {
        var fragment = VideoFrameFragments.Split(new byte[10], 9, 0, false, 3)[0];
        Assert.Multiple(() =>
        {
            Assert.That(fragment[0] & 0xF0, Is.EqualTo(0x10), "the version nibble an older receiver checks is unchanged");
            Assert.That(fragment[0] & 0x03, Is.EqualTo(0x02), "keyframe and last-fragment bits are where they were");
            Assert.That(VideoFrameFragments.Split(new byte[10], 9, 0, true, 3)[0][0] & 0x0C, Is.Zero, "a keyframe is always the base layer");
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
    // The controller itself (step responses, convergence, suspension) is covered in Bolt.Tests
    // SendRateControlTests; these pin down the camera-facing contract Yap relies on.

    private static VideoTier? Place(VideoAdaptation adaptation, int budgetKbps, long nowMs, bool congested = false)
        => adaptation.Rates.Place(budgetKbps, nowMs, congested) is { } setting ? VideoAdaptation.ToTier(setting) : null;

    [Test]
    public void DefaultStartsAt240p15_AndClimbsNoHigherThanThePreference()
    {
        // A call starts inside a 512 kbps mobile budget; the preference is only a ceiling.
        var adaptation = new VideoAdaptation(new MediaServiceOptions().VideoStartTier);
        adaptation.SetCeiling(1080);
        Assert.That(adaptation.Current, Is.EqualTo(new VideoTier(426, 240, 15, 180)));
        for (long now = 0; now < 60_000; now += 250) Place(adaptation, 50_000, now);
        Assert.That(adaptation.Current!.Value.Height, Is.EqualTo(1080));
    }

    [Test]
    public void ABudgetFarBelowThePicture_DropsStraightToTheSizeThatFits()
    {
        var adaptation = new VideoAdaptation(VideoAdaptation.IndexForHeight(1080));
        var tier = Place(adaptation, 330, 0, congested: true);
        Assert.Multiple(() =>
        {
            Assert.That(tier!.Value.Height, Is.EqualTo(360), "one decision, not one rung per two seconds");
            Assert.That(tier.Value.BitrateKbps, Is.EqualTo(330), "the bitrate follows the budget inside the rung");
        });
    }

    [Test]
    public void TheCeilingClampsThePictureImmediatelyAndBlocksFurtherClimbing()
    {
        var adaptation = new VideoAdaptation(VideoAdaptation.IndexForHeight(1080));
        Assert.That(adaptation.SetCeiling(540), Is.True);
        Assert.That(adaptation.Current!.Value.Height, Is.EqualTo(540));
        for (long now = 0; now < 60_000; now += 250) Place(adaptation, 20_000, now);
        Assert.That(adaptation.Current!.Value.Height, Is.EqualTo(540), "a battery or participant cap is not negotiable");
    }

    [Test]
    public void ASuspendedPicture_ReportsNoTier()
    {
        var adaptation = new VideoAdaptation();
        adaptation.Suspended = true;
        Assert.That(adaptation.Current, Is.Null, "null is the UI's 'video paused, audio continues'");
        adaptation.Suspended = false;
        Assert.That(adaptation.Current!.Value.Height, Is.EqualTo(240));
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
    public void A60FpsPreference_IsUsedOnlyWhenTheBudgetAffordsIt_AndGoesFirstUnderPressure()
    {
        var adaptation = new VideoAdaptation(VideoAdaptation.IndexForHeight(720), 60);
        adaptation.SetCeiling(720);
        Assert.That(adaptation.Current!.Value.Framerate, Is.EqualTo(30), "60 fps is earned, not the start");
        for (long now = 0; now < 6_000; now += 250) Place(adaptation, 3_000, now);
        Assert.That(adaptation.Current!.Value.Framerate, Is.EqualTo(60));
        Place(adaptation, 1_200, 6_250, congested: true);
        Assert.That(adaptation.Current, Is.EqualTo(new VideoTier(1280, 720, 30, 1_200)), "frame rate before resolution");
        for (long now = 7_000; now < 20_000; now += 250) Place(adaptation, 3_000, now);
        Assert.That(adaptation.Current!.Value.Framerate, Is.EqualTo(60));
        Assert.That(adaptation.LimitTo30Fps(), Is.True, "an encoder that refuses 60 fps caps the call at 30");
        Assert.That(adaptation.Current!.Value.Framerate, Is.EqualTo(30));
    }

    [Test]
    public void WithoutThePreference_60FpsIsNeverUsed()
    {
        var adaptation = new VideoAdaptation(VideoAdaptation.IndexForHeight(720));
        adaptation.SetCeiling(720);
        for (long now = 0; now < 30_000; now += 250) Place(adaptation, 10_000, now);
        Assert.That(adaptation.Current!.Value.Framerate, Is.EqualTo(30));
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

    // Explicit audio-only acceptance must never opt in to the camera.
    [Test]
    public async Task AnsweringAudioOnly_NeverTurnsTheCameraOn()
    {
        await using var fixture = new VideoFixture(video: true);
        await fixture.Voice.InitializeAsync();
        await fixture.DeliverAsync(fixture.GroupEvent(fixture.Invite(), callerVideo: true));
        Assert.That(fixture.Voice.IncomingHasVideo, Is.True, "the incoming screen should still offer to answer with video");
        await fixture.Voice.AcceptAsync(false);
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Voice.CameraOn, Is.False);
            Assert.That(fixture.Voice.RemoteVideo, Is.Empty);
            Assert.That(fixture.Voice.AnyVideo, Is.False);
        });
    }

    [Test]
    public async Task IncomingVideoIntent_IsShownBeforeTheCallersCameraIsOn()
    {
        await using var fixture = new VideoFixture(video: true);
        await fixture.Voice.InitializeAsync();
        var incoming = fixture.GroupEvent(fixture.Invite());
        await fixture.DeliverAsync(incoming with { Group = incoming.Group! with { VideoRequested = true } });
        Assert.That(fixture.Voice.IncomingHasVideo, Is.True);
        Assert.That(fixture.Voice.CameraOn, Is.False, "an invitation must not open the camera before acceptance");
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
