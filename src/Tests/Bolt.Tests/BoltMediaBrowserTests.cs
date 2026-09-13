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
            opts.VideoWidth = 1920;
            opts.VideoHeight = 1080;
            opts.EnableEncryption = false;
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<MediaServiceOptions>();

        options.AudioBitrateKbps.Should().Be(128);
        options.VideoWidth.Should().Be(1920);
        options.VideoHeight.Should().Be(1080);
        options.EnableEncryption.Should().BeFalse();
    }

    [Test]
    public void MediaServiceOptions_HasCorrectDefaults()
    {
        var options = new MediaServiceOptions();

        options.AudioBitrateKbps.Should().Be(64);
        options.AudioSampleRate.Should().Be(48_000);
        options.AudioChannels.Should().Be(1);
        options.VideoWidth.Should().Be(1280);
        options.VideoHeight.Should().Be(720);
        options.VideoBitrateKbps.Should().Be(2_000);
        options.VideoFramerate.Should().Be(30);
        options.VideoCodec.Should().Be("h264");
        options.KeyframeIntervalFrames.Should().Be(60);
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
}
