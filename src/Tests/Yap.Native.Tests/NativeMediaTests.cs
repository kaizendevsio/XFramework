using NUnit.Framework;
using System.Buffers.Binary;
using System.Security.Authentication;
using System.Text.Json;
using Yap.Native;

namespace Yap.Native.Tests;

public sealed class NativeMediaTests
{
    [Test]
    public async Task Encrypted_pair_delivers_frames_in_both_directions()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var host = new PairingHost("127.0.0.1");
        var accepted = host.AcceptAsync(timeout.Token);
        var connecting = PairingClient.ConnectAsync(PairingInvitation.Parse(host.Invitation.Encode()), timeout.Token);
        try { await Task.WhenAll(accepted, connecting); }
        catch { if (accepted.Exception is not null) TestContext.Error.WriteLine(accepted.Exception); throw; }
        using var client = await connecting;
        using var server = await accepted;
        var picture = new VideoPicture(H264.AnnexB(new[] { new byte[] { 0x65, 1, 2, 3 } }), 123456, true, 270);
        await PictureWire.WriteAsync(client, PictureWire.Pack(picture), timeout.Token);
        var received = PictureWire.Unpack(await PictureWire.ReadAsync(server, timeout.Token));
        Assert.That(received.Data, Is.EqualTo(picture.Data));
        Assert.That(received.Rotation, Is.EqualTo(270));
        await PictureWire.WriteAsync(server, PictureWire.Pack(received), timeout.Token);
        Assert.That(PictureWire.Unpack(await PictureWire.ReadAsync(client, timeout.Token)).TimestampUs, Is.EqualTo(123456));
    }
    [Test]
    public async Task Wrong_certificate_pin_cannot_pair()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var host = new PairingHost("127.0.0.1");
        var accepted = host.AcceptAsync(timeout.Token);
        Assert.ThrowsAsync<AuthenticationException>(async () =>
            await PairingClient.ConnectAsync(host.Invitation with { CertificateSha256 = new string('0', 64) }, timeout.Token));
        try { using var stream = await accepted; } catch (Exception) { }
    }
    [Test]
    public async Task ConnectAsync_UnresponsiveTlsPeer_ReportsHandshakeTimeoutInsteadOfStopped()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var invitation = new PairingInvitation(1, "127.0.0.1",
            ((System.Net.IPEndPoint)listener.LocalEndpoint).Port, new string('0', 64), Convert.ToBase64String(new byte[32]));
        var stages = new List<string>();
        var connecting = PairingClient.ConnectAsync(invitation, timeout.Token, stages.Add);
        using var peer = await listener.AcceptTcpClientAsync(timeout.Token);
        var exception = Assert.ThrowsAsync<TimeoutException>(async () => await connecting);
        Assert.That(exception!.Message, Does.Contain("TLS handshake"));
        Assert.That(stages.Last(), Does.Contain("Host reached"));
    }
    [Test]
    public async Task ConnectAsync_UserCancelsHandshake_PreservesCancellation()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var invitation = new PairingInvitation(1, "127.0.0.1",
            ((System.Net.IPEndPoint)listener.LocalEndpoint).Port, new string('0', 64), Convert.ToBase64String(new byte[32]));
        var connecting = PairingClient.ConnectAsync(invitation, timeout.Token);
        using var peer = await listener.AcceptTcpClientAsync(timeout.Token);
        timeout.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () => await connecting);
    }
    [Test]
    public async Task Wrong_pairing_secret_is_rejected_even_with_valid_certificate()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var host = new PairingHost("127.0.0.1");
        var accepted = host.AcceptAsync(timeout.Token);
        try { using var client = await PairingClient.ConnectAsync(host.Invitation with { Secret = Convert.ToBase64String(new byte[32]) }, timeout.Token); Assert.Fail("Unexpected pairing"); }
        catch (IOException) { }
        Assert.ThrowsAsync<AuthenticationException>(async () => await accepted);
    }
    [TestCase(-1)] [TestCase(0)] [TestCase(PictureWire.MaxBytes + 13)]
    public void Rejects_unbounded_frame_before_allocation(int size)
    {
        var bytes = new byte[4]; BinaryPrimitives.WriteInt32BigEndian(bytes, size);
        Assert.ThrowsAsync<InvalidDataException>(async () => await PictureWire.ReadAsync(new MemoryStream(bytes), default));
    }
    [Test]
    public void Rejects_truncated_stream()
    {
        Assert.ThrowsAsync<EndOfStreamException>(async () => await PictureWire.ReadAsync(new MemoryStream(new byte[] { 0, 0, 0, 10, 1 }), default));
    }
    [Test]
    public void H264_annex_b_and_avcc_preserve_parameter_sets_and_slice_data()
    {
        byte[][] nals = [[0x67, 0x42, 0, 0x28], [0x68, 3, 0, 0, 3, 0], [0x65, 42, 9]];
        var parsed = H264.Nals(H264.AnnexB(nals));
        Assert.That(parsed, Is.EqualTo(nals));
        Assert.That(H264.FromAvcc(H264.Avcc(parsed)), Is.EqualTo(nals));
        Assert.Throws<InvalidDataException>(() => H264.FromAvcc(new byte[] { 0, 0, 4, 0, 1 }));
    }
    [Test]
    public void Camera_settings_and_orientation_are_not_silently_downgraded()
    {
        Assert.Throws<ArgumentException>(() => new VideoSettings(120).Validate());
        Assert.Throws<InvalidDataException>(() => PictureWire.Pack(new([1], 0, true, 45)));
        var command = JsonSerializer.Deserialize("{\"action\":\"join\",\"settings\":{\"fps\":60,\"bitrate\":6000000,\"rear\":true}}", NativeJson.Default.NativeCommand)!;
        Assert.That(command.Settings, Is.EqualTo(new VideoSettings(60, 6_000_000, true)));
    }
    [Test]
    public async Task Native_run_delivers_both_directions_and_releases_cameras_on_stop()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var host = new PairingHost("127.0.0.1");
        var accept = host.AcceptAsync(timeout.Token);
        var client = await PairingClient.ConnectAsync(host.Invitation, timeout.Token); var server = await accept;
        var a = new FakeVideo(); var b = new FakeVideo();
        var runA = new BenchmarkRun(a, server); var runB = new BenchmarkRun(b, client);
        await runA.StartAsync(new()); await runB.StartAsync(new());
        a.Emit(); b.Emit();
        await Task.WhenAll(a.Received.Task, b.Received.Task).WaitAsync(timeout.Token);
        await runA.DisposeAsync(); await runB.DisposeAsync();
        Assert.That(a.Disposed && b.Disposed, Is.True);
    }
    [TestCase(true)]
    [TestCase(false)]
    public async Task Host_BackgroundedDuringInvitation_RemainsPairableAndCapturesOnlyAfterResume(bool connectWhileBackgrounded)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var invitation = new TaskCompletionSource<PairingInvitation>(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var video = new FakeVideo();
        var directory = Path.Combine(Path.GetTempPath(), "yap-native-test-" + Guid.NewGuid().ToString("N"));
        await using var coordinator = new NativeCoordinator(() => Task.FromResult<INativeVideo>(video), directory);
        coordinator.Message += json =>
        {
            using var parsed = JsonDocument.Parse(json);
            var root = parsed.RootElement;
            if (root.GetProperty("kind").GetString() == "invitation")
                invitation.TrySetResult(PairingInvitation.Parse(root.GetProperty("invitation").GetString()!));
            if (root.TryGetProperty("text", out var text))
            {
                if (text.GetString()?.StartsWith("Peer connected.") == true) waiting.TrySetResult();
                if (text.GetString()?.StartsWith("Native video running") == true) started.TrySetResult();
            }
        };
        try
        {
            await coordinator.HandleAsync("{\"action\":\"host\",\"address\":\"127.0.0.1\"}");
            await coordinator.HandleAsync("{\"action\":\"background\"}");
            if (!connectWhileBackgrounded) await coordinator.HandleAsync("{\"action\":\"foreground\"}");
            using var peer = await PairingClient.ConnectAsync(await invitation.Task.WaitAsync(timeout.Token), timeout.Token);
            if (connectWhileBackgrounded)
            {
                await waiting.Task.WaitAsync(timeout.Token);
                Assert.That(started.Task.IsCompleted, Is.False, "Camera must not start in the background");
                await coordinator.HandleAsync("{\"action\":\"foreground\"}");
            }
            await started.Task.WaitAsync(timeout.Token);
            await coordinator.HandleAsync("{\"action\":\"background\"}");
            Assert.That(video.Disposed, Is.True, "An active camera must stop when backgrounded");
        }
        finally
        {
            await coordinator.HandleAsync("{\"action\":\"stop\"}");
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
    private sealed class FakeVideo : INativeVideo
    {
        public TaskCompletionSource Received { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Disposed;
        public event Action<VideoPicture>? Encoded;
        public event Action<string>? Failed { add { } remove { } }
        public VideoCounters Counters { get; } = new();
        public Task StartAsync(VideoSettings settings) => Task.CompletedTask;
        public void Emit() => Encoded?.Invoke(new([0, 0, 0, 1, 0x65, 1], 1, true, 90));
        public void Decode(VideoPicture picture) => Received.TrySetResult();
        public void RequestKeyframe() { }
        public DeviceState DeviceState() => new("unknown", null, null, "fake", "fake", null, null);
        public void Dispose() => Disposed = true;
    }
}
