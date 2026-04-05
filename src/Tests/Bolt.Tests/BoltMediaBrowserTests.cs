using Bolt.Media.Browser;
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
}
