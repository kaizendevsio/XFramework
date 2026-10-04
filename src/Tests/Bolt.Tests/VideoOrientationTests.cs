using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using Bolt.Client;
using Bolt.Media.Browser;
using Bolt.Protocol.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using NUnit.Framework;

namespace Bolt.Tests;

/// <summary>
/// A phone's camera picture leaves as the sensor captured it, with its orientation in the fragment header: inside the
/// SFrame plaintext, so encrypted and authenticated with the picture, and only to a call whose every member announced
/// <see cref="CallMediaFormat.Oriented"/> in its authenticated key envelope. Everyone else gets upright pixels, as before.
/// </summary>
public sealed class VideoOrientationTests
{
    // ── Negotiation ──

    [Test]
    public void OrientationIsSentOnlyWhenEveryPeerAnnouncedIt()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CallMediaFormat.Current, Is.EqualTo(CallMediaFormat.Oriented), "this build reads it");
            Assert.That(CallMediaFormat.SendsOrientation(CallMediaFormat.Common([CallMediaFormat.Oriented, CallMediaFormat.Oriented])), Is.True);
            Assert.That(CallMediaFormat.SendsOrientation(CallMediaFormat.Common([CallMediaFormat.Oriented, CallMediaFormat.Compact])), Is.False,
                "one member on the previous release decides for the call");
            Assert.That(CallMediaFormat.SendsOrientation(CallMediaFormat.Common([CallMediaFormat.Oriented, 0])), Is.False, "nor one that predates the field");
            Assert.That(CallMediaFormat.SendsOrientation(CallMediaFormat.Common([])), Is.False, "nobody announced anything");
            Assert.That(CallMediaFormat.Common([CallMediaFormat.Oriented, CallMediaFormat.Oriented]) >= CallMediaFormat.Compact, Is.True,
                "a member that reads orientation reads compact frames and long Opus packets too");
            Assert.That(CallMediaFormat.CanSend(CallMediaFormat.Compact, 0), Is.True, "an upright picture goes to anyone");
            Assert.That(CallMediaFormat.CanSend(CallMediaFormat.Compact, 1), Is.False, "a turned one only to members that turn it back");
            Assert.That(CallMediaFormat.CanSend(CallMediaFormat.Oriented, 5), Is.True);
        });
    }

    // ── The fragment header ──

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
    public void ATurnedPicture_KeepsItsOrientationThroughSplitAndReassembly(int orientation)
    {
        var picture = RandomNumberGenerator.GetBytes(30_000);
        var fragments = VideoFrameFragments.Split(picture, 9, 1234, isKeyframe: false, layer: 1, orientation: orientation);
        Assert.That(fragments, Has.Count.GreaterThan(1));
        var assembler = new VideoFrameAssembler();
        VideoFramePayload? built = null;
        foreach (var fragment in fragments.AsEnumerable().Reverse()) built ??= assembler.Add(fragment);
        Assert.Multiple(() =>
        {
            Assert.That(fragments.All(x => (x[0] & 0xF0) == 0x20 && x[3] == orientation), Is.True, "version 2: byte 3 is the orientation");
            Assert.That(fragments.Select(x => (int)x[2]), Is.EqualTo(Enumerable.Range(0, fragments.Count)), "byte 2 is the index");
            Assert.That(fragments.All(x => x.Length <= VideoFrameFragments.MaxPlaintext), Is.True, "same header size, same fragment sizes");
            Assert.That(built!.Value.Data, Is.EqualTo(picture));
            Assert.That(built.Value.Orientation, Is.EqualTo(orientation));
            Assert.That(built.Value.Layer, Is.EqualTo(1));
            Assert.That(built.Value.TimestampMicroseconds, Is.EqualTo(1234u));
        });
    }

    [Test]
    public void AnUprightPicture_KeepsTheHeaderEveryReceiverReads()
    {
        var fragments = VideoFrameFragments.Split(new byte[9_000], 2, 0, isKeyframe: true);
        var assembler = new VideoFrameAssembler();
        var built = fragments.Select(x => assembler.Add(x)).Last(x => x is not null);
        Assert.Multiple(() =>
        {
            Assert.That(fragments.All(x => (x[0] & 0xF0) == 0x10 && x[3] == 0), Is.True, "version 1, byte for byte as before");
            Assert.That(built!.Value.Orientation, Is.Zero);
        });
    }

    [Test]
    public void ATurnedPicture_IsUnreadableToAReceiverThatPredatesIt()
    {
        // What a version 1 receiver checks: the version nibble, then the 16-bit index against the count.
        foreach (var fragment in VideoFrameFragments.Split(new byte[9_000], 2, 0, isKeyframe: true, orientation: 1))
            Assert.Multiple(() =>
            {
                Assert.That(fragment[0] & 0xF0, Is.Not.EqualTo(0x10), "an older receiver drops it rather than show it on its side");
                Assert.That(BinaryPrimitives.ReadUInt16LittleEndian(fragment.AsSpan(2)), Is.GreaterThanOrEqualTo(fragment[1] + 1),
                    "and would even without the version: its index would be out of range");
            });
    }

    [Test]
    public void AFragmentThatDisagreesAboutTheOrientation_IsNotPartOfThePicture()
    {
        var fragments = VideoFrameFragments.Split(new byte[9_000], 5, 0, isKeyframe: true, orientation: 1);
        var assembler = new VideoFrameAssembler();
        Assert.That(assembler.Add(fragments[0]), Is.Null);
        var other = (byte[])fragments[1].Clone();
        other[3] = 3;
        Assert.Multiple(() =>
        {
            Assert.That(assembler.Add(other), Is.Null);
            Assert.That(assembler.Incomplete, Is.EqualTo(1), "a sender that cannot agree with itself lost the picture");
        });
    }

    [TestCase(0x08)] [TestCase(0x80)] [TestCase(0xFF)]
    public void UnknownOrientationBits_AreRefused(int value)
    {
        var fragment = VideoFrameFragments.Split(new byte[100], 1, 0, isKeyframe: true, orientation: 1)[0];
        fragment[3] = (byte)value;
        Assert.Multiple(() =>
        {
            Assert.That(new VideoFrameAssembler().Add(fragment), Is.Null);
            Assert.That(() => VideoFrameFragments.Split(new byte[100], 1, 0, true, orientation: value), Throws.InstanceOf<ArgumentOutOfRangeException>());
        });
    }

    [Test]
    public void TheRecoveringReceiver_KeepsEachPicturesOrientation_AndRefusesAMixedOne()
    {
        var buffer = new VideoRecoveryBuffer();
        buffer.Configure(recover: true, rttMs: 200);
        var ready = new List<VideoFramePayload>();
        uint sequence = 100;
        foreach (var (frame, orientation) in new[] { (1u, 1), (2u, 1), (3u, 0), (4u, 3) })
            foreach (var fragment in VideoFrameFragments.Split(new byte[1_000], frame, frame * 33_000, frame == 1, payload: 300, orientation: orientation))
                buffer.Push(sequence++, fragment, 0, ready);
        Assert.That(ready.Select(x => (x.FrameId, x.Orientation)), Is.EqualTo(new[] { (1u, 1), (2u, 1), (3u, 0), (4u, 3) }));

        var mixed = VideoFrameFragments.Split(new byte[1_000], 5, 0, false, payload: 300, orientation: 1);
        mixed[1][3] = 2;
        ready.Clear();
        foreach (var fragment in mixed) buffer.Push(sequence++, fragment, 0, ready);
        Assert.That(ready, Is.Empty);
    }

    // ── The camera is told, from the authenticated envelopes ──

    [Test]
    public async Task TheCameraHearsWhetherToSendOrientation_EvenBeforeItsPipelineExists()
    {
        var js = new ArgsJs();
        await using var pipeline = new BoltVideoPipeline(js, NullLogger<BoltVideoPipeline>.Instance);
        await pipeline.SetOrientationMetadataAsync(true);
        Assert.That(js.Calls.Any(x => x.Identifier == "setOrientationMetadata"), Is.False, "no pipeline yet: nothing to tell");
        await pipeline.InitializeEncoderAsync("vp9", VideoAdaptation.Ladder[0]);
        Assert.That(js.Last("setOrientationMetadata"), Is.EqualTo(new object?[] { true }), "applied as the pipeline is created");
        await pipeline.SetOrientationMetadataAsync(false);
        Assert.That(js.Last("setOrientationMetadata"), Is.EqualTo(new object?[] { false }));
    }

    [Test]
    public async Task OrientationTravelsBothWaysThroughTheInterop()
    {
        var js = new ArgsJs();
        await using var pipeline = new BoltVideoPipeline(js, NullLogger<BoltVideoPipeline>.Instance);
        int? received = null;
        pipeline.OnEncoded += (_, _, _, _, _, orientation) => received = orientation;
        pipeline.OnVideoEncoded([1], false, 1, 0, 0, 5);
        Assert.That(received, Is.EqualTo(5));
        pipeline.OnVideoEncoded([1], false, 2, 0, 0, 0xFD);
        Assert.That(received, Is.EqualTo(5), "only the three defined bits are ever believed");

        await pipeline.InitializeEncoderAsync("vp9", VideoAdaptation.Ladder[0]);
        var stream = Guid.NewGuid();
        await pipeline.DecodeFrameAsync(stream, [1], 10, true, false, 3);
        Assert.That(js.Last("decodeFrame")![^1], Is.EqualTo(3), "the decoded picture is painted turned");
    }

    [Test]
    public async Task InstallingAnEpoch_TurnsMetadataOnOnlyWhenEveryMemberReadsIt()
    {
        var js = new ArgsJs();
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddBoltMediaBrowser(options => options.SecurityMode = MediaSecurityMode.AuthenticatedSFrame);
        await using var provider = services.BuildServiceProvider();
        var media = provider.GetRequiredService<BoltMediaService>();
        await using var client = Client();
        await media.InitializeAsync(client);
        var call = Guid.NewGuid();
        var sender = $"yap-media-{call:N}-{Guid.NewGuid():N}";
        await media.ConfigureSFrameAsync(call, sender);
        await media.JoinHostedGroupAsync(call);
        var local = new SFrameSenderKey(sender, "1", new byte[32]);
        SFrameSenderKey Remote(string kid) => new($"yap-media-{call:N}-{Guid.NewGuid():N}", kid, Enumerable.Repeat((byte)1, 32).ToArray());

        await media.InstallSFrameEpochAsync("1", new string('a', 64), local, [Remote("2")], CallMediaFormat.Common([CallMediaFormat.Oriented]));
        Assert.That(js.Last("setOrientationMetadata"), Is.Null, "no camera pipeline yet; it learns when it is created");
        await media.AttachLocalPreviewAsync(default);
        Assert.That(js.Last("setOrientationMetadata"), Is.EqualTo(new object?[] { true }));

        await media.InstallSFrameEpochAsync("2", new string('a', 64), local with { Kid = "3" }, [Remote("4"), Remote("5")],
            CallMediaFormat.Common([CallMediaFormat.Oriented, CallMediaFormat.Compact]));
        Assert.That(js.Last("setOrientationMetadata"), Is.EqualTo(new object?[] { false }), "a member on the previous release joined");
        Assert.That(media.PeerMediaFormat, Is.EqualTo(CallMediaFormat.Compact));
    }

    private static BoltClient Client()
    {
        var client = new BoltClient(new Uri("wss://example.test/media"), "caller", "Test", new(), NullLogger.Instance);
        var connection = new BoltConnection(new BoltMediaServiceResumeTests.Recording());
        connection.StartSendLoop(CancellationToken.None);
        ((List<BoltConnection>)typeof(BoltClient).GetField("_connections", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!).Add(connection);
        return client;
    }

    /// <summary>A JS runtime that is also every JS object, recording each call with its arguments.</summary>
    private sealed class ArgsJs : IJSInProcessRuntime, IJSObjectReference
    {
        public ConcurrentQueue<(string Identifier, object?[] Args)> Calls { get; } = new();
        public object?[]? Last(string identifier) => Calls.LastOrDefault(x => x.Identifier == identifier).Args;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => ValueTask.FromResult(Invoke<TValue>(identifier, args));
        public TValue Invoke<TValue>(string identifier, params object?[]? args)
        {
            Calls.Enqueue((identifier, args ?? []));
            object? value = typeof(TValue).IsAssignableFrom(typeof(ArgsJs)) ? this
                : typeof(TValue) == typeof(bool) ? true
                : typeof(TValue) == typeof(byte[]) ? new byte[16]
                : typeof(TValue) == typeof(VoiceCapabilities) ? new VoiceCapabilities(true, null, true)
                : typeof(TValue) == typeof(VideoCapabilities) ? new VideoCapabilities(true, null, 1080, [])
                : default(TValue);
            return (TValue)value!;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
