using System.Collections.Concurrent;
using System.Reflection;
using Bolt.Client;
using Bolt.Media.Browser;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using NUnit.Framework;

namespace Bolt.Tests;

/// <summary>
/// The browser media service across a transport swap: a hosted SFrame call survives its socket,
/// keeps its keys and capture, and republishes fresh streams on the next connection.
/// </summary>
public sealed class BoltMediaServiceResumeTests
{
    [Test]
    public async Task LostSocket_DoesNotEndAHostedSFrameCall()
    {
        await using var f = await Fixture.CreateAsync();
        RaiseDisconnected(f.First);
        await Task.Delay(200);
        Assert.Multiple(() =>
        {
            Assert.That(f.Js.Calls, Does.Not.Contain("dispose"), "the SFrame session and its sender counter live on");
            Assert.That(f.Js.Calls, Does.Not.Contain("stopCapture"), "the microphone is not released by a network blip");
            Assert.That(f.Media.HasTransport, Is.True, "the host, not the socket, decides when to let go");
        });
    }

    [Test]
    public async Task Resume_SwapsTransport_KeepsTheEpoch_AndRepublishesAFreshStream()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Media.StartHostedAudioAsync(f.Call);
        var before = f.FirstTransport.AudioConfigs();
        Assert.That(before, Has.Count.EqualTo(1));

        await f.Media.SuspendTransportAsync();
        Assert.Multiple(() =>
        {
            Assert.That(f.Media.HasTransport, Is.False);
            Assert.That(f.Media.LastInboundTick, Is.Null);
            Assert.That(f.Media.IsSFrameReady, Is.True, "the epoch stays active across the swap: no rekey, no nonce reuse");
        });
        Assert.That(await f.Media.SendHeartbeatAsync(f.Call, 1), Is.False, "nothing to probe while detached");

        var (second, transport) = Fixture.Client();
        f.Media.AttachTransport(second);
        Assert.Throws<InvalidOperationException>(() => f.Media.AttachTransport(second), "one transport at a time");
        await f.Media.JoinHostedGroupAsync(f.Call);
        await f.Media.StartHostedAudioAsync(f.Call);

        var after = transport.AudioConfigs();
        Assert.Multiple(() =>
        {
            Assert.That(after, Has.Count.EqualTo(1), "the audio stream is published again on the new connection");
            Assert.That(after[0], Is.Not.EqualTo(before[0]), "as a new stream, so every receiver starts it fresh");
            Assert.That(f.Js.Calls.Count(x => x == "createSession"), Is.EqualTo(1), "on the same SFrame session");
            Assert.That(f.Js.Calls.Count(x => x == "installEpoch"), Is.EqualTo(1), "with the epoch it installed once");
        });
        Assert.That(await f.Media.SendHeartbeatAsync(f.Call, 2), Is.True);
        await second.DisposeAsync();
    }

    [Test]
    public async Task Resume_RepublishesTheCameraWithoutReopeningIt_AndStartsOnAKeyframe()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Media.StartHostedAudioAsync(f.Call);
        await f.Media.StartVideoAsync(f.Call, VideoCodec.H264, 720);
        var first = f.FirstTransport.Configs(MediaType.Video);
        Assert.That(first, Has.Count.EqualTo(1));

        await f.Media.SuspendTransportAsync();
        Assert.That(await f.Media.ResumeVideoStreamAsync(f.Call), Is.False, "no transport, nothing to publish on");
        var (second, transport) = Fixture.Client();
        f.Media.AttachTransport(second);
        await f.Media.JoinHostedGroupAsync(f.Call);
        await f.Media.StartHostedAudioAsync(f.Call);
        var opened = f.Js.Calls.Count(x => x == "startCapture");
        var keyframes = f.Js.Calls.Count(x => x == "requestKeyframe");

        Assert.That(await f.Media.ResumeVideoStreamAsync(f.Call), Is.True);
        Assert.That(await f.Media.ResumeVideoStreamAsync(f.Call), Is.False, "published once");
        var again = transport.Configs(MediaType.Video);
        Assert.Multiple(() =>
        {
            Assert.That(again, Has.Count.EqualTo(1));
            Assert.That(again[0], Is.Not.EqualTo(first[0]), "a new stream on the new connection");
            Assert.That(f.Js.Calls.Count(x => x == "startCapture"), Is.EqualTo(opened), "the camera was never closed, so it is not reopened");
            Assert.That(f.Js.Calls.Count(x => x == "requestKeyframe"), Is.GreaterThan(keyframes), "receivers start on a keyframe");
            Assert.That(f.Media.IsCameraOn, Is.True);
        });
        await second.DisposeAsync();
    }

    [Test]
    public async Task AttachTransport_RequiresWss()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Media.SuspendTransportAsync();
        await using var plain = new BoltClient(new Uri("ws://example.test/media"), "caller", "Test", new(), NullLogger.Instance);
        Assert.Throws<InvalidOperationException>(() => f.Media.AttachTransport(plain));
    }

    private static void RaiseDisconnected(BoltClient client) =>
        ((Action?)typeof(BoltClient).GetField("Disconnected", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client))?.Invoke();

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid Call { get; } = Guid.NewGuid();
        public RecordingJs Js { get; } = new();
        public BoltMediaService Media { get; private set; } = null!;
        public BoltClient First { get; private set; } = null!;
        public Recording FirstTransport { get; private set; } = null!;
        private ServiceProvider provider = null!;

        public static async Task<Fixture> CreateAsync()
        {
            var f = new Fixture();
            var services = new ServiceCollection();
            services.AddSingleton<IJSRuntime>(f.Js);
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.AddBoltMediaBrowser(options => options.SecurityMode = MediaSecurityMode.AuthenticatedSFrame);
            f.provider = services.BuildServiceProvider();
            f.Media = f.provider.GetRequiredService<BoltMediaService>();
            (f.First, f.FirstTransport) = Client();
            await f.Media.InitializeAsync(f.First);
            var sender = $"yap-media-{f.Call:N}-{Guid.NewGuid():N}";
            await f.Media.ConfigureSFrameAsync(f.Call, sender);
            await f.Media.JoinHostedGroupAsync(f.Call);
            var local = new SFrameSenderKey(sender, "1", new byte[32]);
            var remote = new SFrameSenderKey($"yap-media-{f.Call:N}-{Guid.NewGuid():N}", "2", Enumerable.Repeat((byte)1, 32).ToArray());
            await f.Media.InstallSFrameEpochAsync("1", new string('a', 64), local, [remote]);
            await f.Media.ActivateSFrameEpochAsync("1", new string('a', 64));
            return f;
        }

        public static (BoltClient, Recording) Client()
        {
            var client = new BoltClient(new Uri("wss://example.test/media"), "caller", "Test", new(), NullLogger.Instance);
            var transport = new Recording();
            var connection = new BoltConnection(transport);
            connection.StartSendLoop(CancellationToken.None);
            ((List<BoltConnection>)typeof(BoltClient).GetField("_connections", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!).Add(connection);
            return (client, transport);
        }

        public async ValueTask DisposeAsync()
        {
            await provider.DisposeAsync();
            await First.DisposeAsync();
        }
    }

    public sealed class Recording : IBoltConnection
    {
        public ConcurrentQueue<byte[]> Sent { get; } = new();
        public List<Guid> AudioConfigs() => Configs(MediaType.Audio);
        public List<Guid> Configs(MediaType type)
        {
            var deadline = DateTime.UtcNow.AddSeconds(2);
            bool Match(byte[] x) => BoltCodec.TryReadMediaConfig(x, out var c) && c.MediaType == type;
            while (DateTime.UtcNow < deadline && !Sent.Any(Match)) Thread.Sleep(10);
            return Sent.Where(Match)
                .Select(x => { BoltCodec.TryReadMediaConfig(x, out var c); return c.StreamId; }).ToList();
        }
        public bool SupportsDatagrams => false;
        public bool IsConnected => true;
        public BoltTransport TransportType => BoltTransport.WebSocket;
        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) { Sent.Enqueue(data.ToArray()); return ValueTask.CompletedTask; }
        public async ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default)
        { await Task.Delay(Timeout.Infinite, ct); return (0, true); }
        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask CloseAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>Records every interop call by name and hands itself out as any imported module or session.</summary>
    public sealed class RecordingJs : IJSInProcessRuntime, IJSObjectReference
    {
        public ConcurrentQueue<string> Calls { get; } = new();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => ValueTask.FromResult(Invoke<TValue>(identifier, args));
        public TValue Invoke<TValue>(string identifier, params object?[]? args)
        {
            Calls.Enqueue(identifier);
            object? value = typeof(TValue).IsAssignableFrom(typeof(RecordingJs)) ? this
                : typeof(TValue) == typeof(bool) ? true
                : typeof(TValue) == typeof(byte[]) ? new byte[16]
                : typeof(TValue) == typeof(VoiceCapabilities) ? new VoiceCapabilities(true, null, true)
                : typeof(TValue) == typeof(VideoCaptureState) ? CameraState()
                : default(TValue);
            return (TValue)value!;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static VideoCaptureState CameraState() =>
            (VideoCaptureState)System.Text.Json.JsonSerializer.Deserialize("{\"capturing\":true,\"width\":426,\"height\":240}", typeof(VideoCaptureState),
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
}
