using System.Diagnostics;
using System.Net.Security;
using System.Threading.Channels;

namespace Yap.Native;

public sealed class BenchmarkRun : IAsyncDisposable
{
    private readonly INativeVideo video;
    private readonly SslStream transport;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Channel<VideoPicture> outgoing = Channel.CreateBounded<VideoPicture>(new BoundedChannelOptions(2)
        { SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait });
    private int needsKeyframe = 1;
    private Task completion = Task.CompletedTask;
    private string? failure;
    private bool disposed;
    public Guid Id { get; } = Guid.NewGuid();
    public Task Completion => completion;
    public event Action<NativeSample>? Sample;
    public event Action<string>? Ended;
    public BenchmarkRun(INativeVideo video, SslStream transport) { this.video = video; this.transport = transport; }
    public async Task StartAsync(VideoSettings settings)
    {
        settings.Validate();
        video.Encoded += Send;
        video.Failed += Fail;
        await video.StartAsync(settings);
        completion = RunAsync(settings);
    }
    private void Fail(string message) { failure = message; lifetime.Cancel(); }
    private void Send(VideoPicture picture)
    {
        Interlocked.Increment(ref video.Counters.Encoded);
        if (Volatile.Read(ref needsKeyframe) != 0 && !picture.Keyframe) return;
        if (!outgoing.Writer.TryWrite(picture))
        {
            Interlocked.Increment(ref video.Counters.QueueDrops);
            Volatile.Write(ref needsKeyframe, 1); video.RequestKeyframe(); return;
        }
        if (picture.Keyframe) Volatile.Write(ref needsKeyframe, 0);
    }
    private async Task RunAsync(VideoSettings settings)
    {
        var tasks = new[] { WriteAsync(), ReadAsync(), SampleAsync(settings) };
        try { await await Task.WhenAny(tasks); }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex) { failure = ex.Message; }
        finally
        {
            lifetime.Cancel(); transport.Dispose();
            try { await Task.WhenAll(tasks); } catch { /* first failure reported below */ }
            Ended?.Invoke(failure ?? "Stopped");
        }
    }
    private async Task WriteAsync()
    {
        await foreach (var picture in outgoing.Reader.ReadAllAsync(lifetime.Token))
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            var data = PictureWire.Pack(picture);
            await PictureWire.WriteAsync(transport, data, timeout.Token);
            Interlocked.Add(ref video.Counters.BytesSent, data.Length);
        }
    }
    private async Task ReadAsync()
    {
        while (!lifetime.IsCancellationRequested)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var data = await PictureWire.ReadAsync(transport, timeout.Token);
            Interlocked.Add(ref video.Counters.BytesReceived, data.Length);
            Interlocked.Increment(ref video.Counters.Received);
            video.Decode(PictureWire.Unpack(data));
        }
    }
    private async Task SampleAsync(VideoSettings settings)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        // System.Diagnostics.Process metrics are unsupported on iOS. Optional
        // telemetry must not terminate the media run or report fabricated zeros.
        using var process = OperatingSystem.IsIOS() ? null : Process.GetCurrentProcess();
        var start = Stopwatch.GetTimestamp(); var previous = start;
        var cpu = process?.TotalProcessorTime;
        long captured = 0, encoded = 0, received = 0, output = 0, sentBytes = 0, receivedBytes = 0;
        while (await timer.WaitForNextTickAsync(lifetime.Token))
        {
            var now = Stopwatch.GetTimestamp(); var elapsed = Stopwatch.GetElapsedTime(previous, now).TotalSeconds; previous = now;
            process?.Refresh(); var nextCpu = process?.TotalProcessorTime;
            var c = video.Counters;
            var nc = Interlocked.Read(ref c.Captured); var ne = Interlocked.Read(ref c.Encoded);
            var nr = Interlocked.Read(ref c.Received); var no = Interlocked.Read(ref c.Output);
            var ns = Interlocked.Read(ref c.BytesSent); var nb = Interlocked.Read(ref c.BytesReceived);
            Sample?.Invoke(new NativeSample
            {
                Run = Id, Time = DateTimeOffset.UtcNow,
                Seconds = Stopwatch.GetElapsedTime(start, now).TotalSeconds, Settings = settings,
                CaptureFps = (nc - captured) / elapsed, EncodedFps = (ne - encoded) / elapsed,
                ReceivedFps = (nr - received) / elapsed, OutputFps = (no - output) / elapsed,
                CaptureMaxGapMs = c.CaptureGapMs(), OutputMaxGapMs = c.OutputGapMs(),
                SentKbps = (ns - sentBytes) * 8 / 1000d / elapsed, ReceivedKbps = (nb - receivedBytes) * 8 / 1000d / elapsed,
                Queue = outgoing.Reader.Count, QueueDrops = Interlocked.Read(ref c.QueueDrops),
                CpuCorePercent = nextCpu.HasValue && cpu.HasValue ? (nextCpu.Value - cpu.Value).TotalSeconds / elapsed * 100 : null,
                MemoryBytes = process?.WorkingSet64, Device = video.DeviceState()
            });
            captured = nc; encoded = ne; received = nr; output = no; sentBytes = ns; receivedBytes = nb; cpu = nextCpu;
            if (Stopwatch.GetElapsedTime(start, now) > TimeSpan.FromMinutes(15)) { lifetime.Cancel(); return; }
        }
    }
    public async ValueTask DisposeAsync()
    {
        if (disposed) return; disposed = true;
        video.Encoded -= Send; video.Failed -= Fail;
        lifetime.Cancel(); transport.Dispose();
        await completion;
        video.Dispose(); lifetime.Dispose();
    }
}
