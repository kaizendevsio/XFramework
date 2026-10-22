using System.Text.Json;

namespace Yap.Native;

public sealed record NativeCommand(string Action, string? Address, string? Invitation, VideoSettings? Settings);

public sealed class NativeCoordinator(Func<Task<INativeVideo>> createVideo, string outputDirectory) : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1);
    private CancellationTokenSource? lifetime;
    private Task job = Task.CompletedTask;
    private volatile bool waitingForPeer;
    private TaskCompletionSource? foregroundResume;
    public event Action<string>? Message;
    public string? LastReport { get; private set; }
    public async Task HandleAsync(string json)
    {
        if (json.Length > 4096) throw new ArgumentException("Command too large.");
        var command = JsonSerializer.Deserialize(json, NativeJson.Default.NativeCommand)
            ?? throw new ArgumentException("Invalid command.");
        await gate.WaitAsync();
        try
        {
            if (command.Action == "foreground")
            {
                foregroundResume?.TrySetResult(); foregroundResume = null; return;
            }
            if (command.Action == "background")
            {
                foregroundResume ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
                // Copying an invitation into another app must not close its listener.
                // Active video still stops as soon as the app leaves the foreground.
                if (!waitingForPeer) await StopAsync();
                return;
            }
            if (command.Action == "stop") { await StopAsync(); return; }
            if (command.Action is not ("host" or "join")) throw new ArgumentException("Unknown command.");
            if (!job.IsCompleted) throw new InvalidOperationException("Stop the current test first.");
            var settings = command.Settings ?? new(); settings.Validate();
            await StopAsync(); lifetime = new();
            if (command.Action == "host")
            {
                var host = new PairingHost(command.Address ?? "");
                waitingForPeer = true;
                Notify(new("invitation", Invitation: host.Invitation.Encode()));
                job = RunAsync(host.AcceptAsync(lifetime.Token), settings, host, lifetime.Token);
            }
            else job = RunAsync(PairingClient.ConnectAsync(PairingInvitation.Parse(command.Invitation ?? ""), lifetime.Token), settings, null, lifetime.Token);
        }
        finally { gate.Release(); }
    }
    private async Task RunAsync(Task<System.Net.Security.SslStream> connected, VideoSettings settings, PairingHost? host, CancellationToken ct)
    {
        INativeVideo? video = null;
        try
        {
            Notify(new("status", "Waiting for encrypted peer connection"));
            using var stream = await connected; ct.ThrowIfCancellationRequested();
            waitingForPeer = false;
            if (foregroundResume is { } resume)
            {
                Notify(new("status", "Peer connected. Return to the benchmark app to start the camera."));
                await resume.Task.WaitAsync(TimeSpan.FromMinutes(3), ct);
            }
            ct.ThrowIfCancellationRequested();
            video = await createVideo(); ct.ThrowIfCancellationRequested();
            await using var run = new BenchmarkRun(video, stream);
            Directory.CreateDirectory(outputDirectory);
            LastReport = Path.Combine(outputDirectory, $"native-{run.Id:N}.jsonl");
            using var report = new StreamWriter(LastReport);
            run.Sample += sample => { var json = JsonSerializer.Serialize(sample, NativeJson.Default.NativeSample); report.WriteLine(json); report.Flush(); Message?.Invoke(json); };
            run.Ended += text => Notify(new("status", text));
            await run.StartAsync(settings);
            Notify(new("status", "Native video running — microphones off"));
            try { await run.Completion.WaitAsync(ct); } catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            finally { await run.DisposeAsync(); } // Stop sampling before the report writer is disposed.
        }
        catch (OperationCanceledException) { Notify(new("status", "Stopped")); }
        catch (Exception ex) { Notify(new("error", ex.Message)); }
        finally { waitingForPeer = false; video?.Dispose(); host?.Dispose(); Notify(new("finished")); }
    }
    private void Notify(NativeNotice notice) => Message?.Invoke(JsonSerializer.Serialize(notice, NativeJson.Default.NativeNotice));
    private async Task StopAsync()
    {
        lifetime?.Cancel(); await job; lifetime?.Dispose(); lifetime = null;
    }
    public async ValueTask DisposeAsync() { await gate.WaitAsync(); try { await StopAsync(); } finally { gate.Release(); } }
}
