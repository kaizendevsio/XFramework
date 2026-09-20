using Bolt.Media.Browser;
using Bolt.Client;
using System.Buffers.Binary;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using NSubstitute;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public class BoltMediaBrowserTests
{
    [Test]
    public void AddBoltMediaBrowser_RegistersAllServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(Substitute.For<IJSRuntime>());
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddBoltMediaBrowser();

        var provider = services.BuildServiceProvider();

        provider.GetService<BoltCryptoInterop>().Should().NotBeNull();
        provider.GetService<BoltAudioPipeline>().Should().NotBeNull();
        provider.GetService<BoltVideoPipeline>().Should().NotBeNull();
        provider.GetService<BoltDeviceManager>().Should().NotBeNull();
        provider.GetService<BoltMediaService>().Should().NotBeNull();
        provider.GetService<MediaServiceOptions>().Should().NotBeNull();
    }

    [Test]
    public void AddBoltMediaBrowser_WithOptions_AppliesConfiguration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(Substitute.For<IJSRuntime>());
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddBoltMediaBrowser(opts =>
        {
            opts.AudioBitrateKbps = 128;
            opts.VideoMaxHeight = 1080;
            opts.VideoStartTier = 5;
            opts.EnableEncryption = false;
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<MediaServiceOptions>();

        options.AudioBitrateKbps.Should().Be(128);
        options.VideoMaxHeight.Should().Be(1080);
        options.VideoStartTier.Should().Be(5);
        options.EnableEncryption.Should().BeFalse();
    }

    [Test]
    public void MediaServiceOptions_HasCorrectDefaults()
    {
        var options = new MediaServiceOptions();

        options.AudioBitrateKbps.Should().Be(128);
        options.AudioSampleRate.Should().Be(48_000);
        options.AudioChannels.Should().Be(1);
        options.VideoMaxHeight.Should().Be(1080);
        // The ladder opens at 720p30 and climbs; see VideoAdaptation for why it does not open at 1080p.
        VideoAdaptation.Ladder[options.VideoStartTier].Height.Should().Be(720);
        options.KeyframeIntervalSeconds.Should().Be(2);
        options.EnableEncryption.Should().BeTrue();
        options.EnableFec.Should().BeTrue();
        options.FecAudioGroupSize.Should().Be(4);
        options.FecVideoGroupSize.Should().Be(8);
    }

    [Test]
    public void CryptoInterop_CreateEncryption_ThrowsBeforeInit()
    {
        var js = Substitute.For<IJSRuntime>();
        var crypto = new BoltCryptoInterop(js);

        var act = () => crypto.CreateEncryption();
        act.Should().Throw<InvalidOperationException>().WithMessage("*InitializeAsync*");
    }

    [Test]
    public async Task MediaService_StartCall_ThrowsBeforeInit()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(Substitute.For<IJSRuntime>());
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddBoltMediaBrowser();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<BoltMediaService>();

        var act = async () => await service.StartCallAsync("someone");
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*InitializeAsync*");
    }

    [Test]
    public async Task MediaService_LegacyEncryptionFalse_DoesNotOptIntoTransportSecurity()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IJSRuntime>());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddBoltMediaBrowser(options => options.EnableEncryption = false);
        await using var provider = services.BuildServiceProvider();
        await using var client = new BoltClient(new Uri("wss://example.test/media"), "caller", "Test", new(), NullLogger.Instance);

        var initialize = () => provider.GetRequiredService<BoltMediaService>().InitializeAsync(client);

        await initialize.Should().ThrowAsync<NotSupportedException>().WithMessage("*authenticated peer identities*");
    }

    [Test]
    public async Task MediaService_TransportSecurity_RejectsPlaintextEndpoint()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IJSRuntime>());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddBoltMediaBrowser(options => options.SecurityMode = MediaSecurityMode.AuthenticatedTransport);
        await using var provider = services.BuildServiceProvider();
        await using var client = new BoltClient(new Uri("ws://example.test/media"), "caller", "Test", new(), NullLogger.Instance);

        var initialize = () => provider.GetRequiredService<BoltMediaService>().InitializeAsync(client);

        await initialize.Should().ThrowAsync<InvalidOperationException>().WithMessage("*WSS*");
    }

    [Test]
    public async Task AudioPipeline_EncodedCallback_AwaitsTransportCompletion()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pipeline = new BoltAudioPipeline(Substitute.For<IJSRuntime>(), NullLogger<BoltAudioPipeline>.Instance);
        pipeline.OnEncoded += _ => completion.Task;

        var pending = pipeline.OnAudioEncoded([1, 2, 3]);

        pending.IsCompleted.Should().BeFalse();
        completion.SetResult();
        await pending;
    }

    [Test]
    public void ManagedOpusCodec_EncodesAndDecodesVoiceWithoutNativeLibrary()
    {
        using var sender = new ManagedOpusCodec();
        using var receiver = new ManagedOpusCodec();
        sender.BitrateKbps.Should().Be(128);
        var pcm = new byte[1920];
        for (var i = 0; i < 960; i++)
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i * 2), (short)(Math.Sin(2 * Math.PI * 440 * i / 48000) * 12000));

        var packet = sender.Encode(pcm);
        var decoded = receiver.Decode(packet);

        packet.Length.Should().BeInRange(1, 1275);
        decoded.Length.Should().Be(1920);
        decoded.Any(sample => sample != 0).Should().BeTrue();
    }

    [Test]
    public void ManagedOpusCodec_RejectsUnboundedOrInvalidFrameSizes()
    {
        using var codec = new ManagedOpusCodec();

        var encode = () => codec.Encode(new byte[3840]);
        var decode = () => codec.Decode(new byte[1276]);
        var empty = () => codec.Decode([]);

        encode.Should().Throw<ArgumentException>();
        decode.Should().Throw<ArgumentException>();
        empty.Should().Throw<ArgumentException>();
    }

    [Test]
    public void ManagedOpusDecoder_InterleavedSpeakersRetainIndependentHistory()
    {
        using var alice = new ManagedOpusCodec();
        using var bob = new ManagedOpusCodec();
        using var aliceReceiver = new ManagedOpusDecoder();
        using var bobReceiver = new ManagedOpusDecoder();
        using var aliceReference = new ManagedOpusDecoder();
        using var bobReference = new ManagedOpusDecoder();
        var alicePcm = new byte[1920];
        var bobPcm = new byte[1920];
        for (var frame = 0; frame < 20; frame++)
        {
            for (var sample = 0; sample < 960; sample++)
            {
                var time = (frame * 960 + sample) / 48000d;
                BinaryPrimitives.WriteInt16LittleEndian(alicePcm.AsSpan(sample * 2), (short)(Math.Sin(2 * Math.PI * 440 * time) * 12000));
                BinaryPrimitives.WriteInt16LittleEndian(bobPcm.AsSpan(sample * 2), (short)(Math.Sin(2 * Math.PI * 880 * time) * 9000));
            }
            var alicePacket = alice.Encode(alicePcm);
            var bobPacket = bob.Encode(bobPcm);
            aliceReceiver.Decode(alicePacket).Should().Equal(aliceReference.Decode(alicePacket));
            bobReceiver.Decode(bobPacket).Should().Equal(bobReference.Decode(bobPacket));
        }
    }

    [Test]
    public async Task AudioPipeline_ManagedGroupRoutesIndependentPcmAndBoundsReceivers()
    {
        var js = Substitute.For<IJSRuntime>();
        var module = Substitute.For<IJSObjectReference>();
        var browser = Substitute.For<IJSObjectReference>();
        js.InvokeAsync<IJSObjectReference>("import", Arg.Any<object?[]>()).Returns(module);
        module.InvokeAsync<IJSObjectReference>("createAudioPipeline", Arg.Any<object?[]>()).Returns(browser);
        module.InvokeAsync<VoiceCapabilities>("checkVoiceCapabilities", Arg.Any<object?[]>())
            .Returns(new VoiceCapabilities(true, null, false));
        await using var pipeline = new BoltAudioPipeline(js, NullLogger<BoltAudioPipeline>.Instance);
        await pipeline.InitializeAsync();
        using var sender = new ManagedOpusCodec();
        using var reference = new ManagedOpusDecoder();
        var packet = sender.Encode(new byte[1920]);
        var expected = reference.Decode(packet);
        var streamIds = Enumerable.Range(0, 9).Select(_ => Guid.NewGuid()).ToArray();

        foreach (var streamId in streamIds) await pipeline.DecodeFrameAsync(streamId, packet, 0);

        var playCalls = browser.ReceivedCalls().Where(call => Equals(call.GetArguments()[0], "playPcm")).ToArray();
        playCalls.Should().HaveCount(8);
        foreach (var call in playCalls)
        {
            var arguments = (object?[])call.GetArguments()[1]!;
            ((byte[])arguments[0]!).Should().Equal(expected);
        }
        await pipeline.ReleaseRemoteStreamAsync(streamIds[0]);
        await pipeline.DecodeFrameAsync(streamIds[8], packet, 0);
        browser.ReceivedCalls().Count(call => Equals(call.GetArguments()[0], "playPcm")).Should().Be(9);
    }
}
