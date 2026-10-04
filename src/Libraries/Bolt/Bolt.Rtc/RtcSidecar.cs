using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bolt.Protocol.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bolt.Rtc;

public sealed class RtcSidecarOptions
{
    /// <summary>The bolt-rtc executable. Defaults to bolt-rtc (bolt-rtc.exe on Windows) next to the application.</summary>
    public string? ExecutablePath { get; set; }
    /// <summary>Most peers the sidecar serves at once.</summary>
    public int MaxSessions { get; set; } = 256;
    public TimeSpan StartupTimeout { get; set; } = TimeSpan.FromSeconds(10);
    /// <summary>A sidecar that keeps crashing is not restarted more often than this.</summary>
    public TimeSpan RestartBackoff { get; set; } = TimeSpan.FromSeconds(5);
    /// <summary>
    /// How many sockets each UDP TURN allocation starts from; the first the TURN server answers is kept. Null keeps the
    /// sidecar's default (16): some networks drop most UDP flows to a TURN anycast address but pass others. 1 is
    /// pion's own single socket, for tests that show the difference.
    /// </summary>
    public int? TurnFlows { get; set; }

    public string ResolveExecutable() => ExecutablePath is { Length: > 0 } path
        ? path
        : Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "bolt-rtc.exe" : "bolt-rtc");
}

/// <summary>
/// Runs the bolt-rtc sidecar and hands out peers that are driven through it. The process is started on
/// the first peer, restarted (with backoff) after it dies, and stopped with this object. It listens on a
/// Unix socket in a directory only this user can enter, and every connection must present a random token
/// that only this process gave it.
///
/// Nothing here blocks the relay: peers are created on signalling, never on the media path, and a dead
/// sidecar only means the next attempt gets a fresh one while calls carry on over their WebSockets.
/// </summary>
public sealed class RtcSidecar : IRtcPeerFactory, IAsyncDisposable
{
    private readonly RtcSidecarOptions _options;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private readonly string _token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private Process? _process;
    private string? _directory;
    private string? _socketPath;
    private long _nextStartAllowed;
    private bool _disposed;

    public RtcSidecar(RtcSidecarOptions options, ILogger<RtcSidecar>? logger = null)
    {
        _options = options;
        _logger = (ILogger?)logger ?? NullLogger.Instance;
    }

    /// <summary>True while the executable exists; a host without it simply never offers the datagram path.</summary>
    public bool IsAvailable => File.Exists(_options.ResolveExecutable());

    public bool IsRunning => _process is { HasExited: false };

    /// <summary>Tests: the running sidecar's process ID.</summary>
    internal int? ProcessId => _process is { HasExited: false } process ? process.Id : null;

    public async ValueTask<IRtcPeer> CreateAsync(RtcPeerRole role, RtcPeerOptions options, CancellationToken ct)
    {
        var socketPath = await EnsureStartedAsync(ct);
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            try { await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct); }
            catch (SocketException) when (_process is { } process)
            {
                // The sidecar died and its peers noticed before the process object did: its socket refuses. Let the
                // exit land, start a new one and connect to that instead (once).
                try { process.WaitForExit(1000); } catch { /* Gone. */ }
                socket.Dispose();
                socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                socketPath = await EnsureStartedAsync(ct);
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct);
            }
            var stream = new NetworkStream(socket, ownsSocket: true);
            var hello = JsonSerializer.SerializeToUtf8Bytes(new SidecarHello(
                _token, role == RtcPeerRole.Offer ? "offer" : "answer",
                options.IceServers.Select(x => new SidecarIceServer(x.Urls, x.Username, x.Credential)).ToArray(),
                options.RelayOnly, options.MaxMessageBytes, options.MinCwndBytes, options.AllowLoopback, _options.TurnFlows), SidecarJson.Default.SidecarHello);
            var peer = new SidecarRtcPeer(stream, role, options.MaxMessageBytes, _logger);
            await peer.StartAsync(hello, ct);
            return peer;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private async ValueTask<string> EnsureStartedAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_process is { HasExited: false } && _socketPath is { } running)
            return running;
        await _startGate.WaitAsync(ct);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_process is { HasExited: false } && _socketPath is { } current)
                return current;
            if (Environment.TickCount64 < _nextStartAllowed)
                throw new InvalidOperationException("The WebRTC sidecar is restarting.");
            _nextStartAllowed = Environment.TickCount64 + (long)_options.RestartBackoff.TotalMilliseconds;
            StopProcess();
            return await StartProcessAsync(ct);
        }
        finally { _startGate.Release(); }
    }

    private async Task<string> StartProcessAsync(CancellationToken ct)
    {
        var executable = _options.ResolveExecutable();
        if (!File.Exists(executable))
            throw new FileNotFoundException("The bolt-rtc sidecar is not installed.", executable);

        var directory = Path.Combine(Path.GetTempPath(), "bolt-rtc-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant());
        if (OperatingSystem.IsWindows())
            Directory.CreateDirectory(directory);
        else
            Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var socketPath = Path.Combine(directory, "rtc.sock");

        var start = new ProcessStartInfo(executable)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("-socket");
        start.ArgumentList.Add(socketPath);
        start.ArgumentList.Add("-max-sessions");
        start.ArgumentList.Add(Math.Clamp(_options.MaxSessions, 1, 4096).ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.Environment["BOLT_RTC_TOKEN"] = _token;

        var process = Process.Start(start) ?? throw new InvalidOperationException("The WebRTC sidecar did not start.");
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        process.OutputDataReceived += (_, line) => { if (line.Data == "READY") ready.TrySetResult(); };
        // The sidecar logs session numbers and states only: no SDP, candidates or credentials.
        process.ErrorDataReceived += (_, line) => { if (line.Data is { Length: > 0 } text) _logger.LogInformation("{Line}", text); };
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) =>
        {
            ready.TrySetException(new InvalidOperationException("The WebRTC sidecar exited during startup."));
            if (!_disposed) _logger.LogWarning("The WebRTC sidecar exited (code {Code}); calls continue over WebSockets", SafeExitCode(process));
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try
        {
            await ready.Task.WaitAsync(_options.StartupTimeout, ct);
        }
        catch
        {
            try { process.Kill(entireProcessTree: true); } catch { /* Already gone. */ }
            process.Dispose();
            TryDeleteDirectory(directory);
            throw;
        }
        _process = process;
        _directory = directory;
        _socketPath = socketPath;
        _logger.LogInformation("WebRTC sidecar started");
        return socketPath;
    }

    private static int? SafeExitCode(Process process)
    {
        try { return process.ExitCode; } catch { return null; }
    }

    private void StopProcess()
    {
        var process = _process;
        _process = null;
        _socketPath = null;
        if (process is not null)
        {
            try
            {
                // Closing stdin asks it to exit; kill if it does not within a second.
                process.StandardInput.Close();
                if (!process.WaitForExit(1000)) process.Kill(entireProcessTree: true);
            }
            catch { /* Already gone. */ }
            process.Dispose();
        }
        if (_directory is { } directory)
        {
            _directory = null;
            TryDeleteDirectory(directory);
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try { Directory.Delete(directory, recursive: true); } catch { /* Temporary. */ }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await _startGate.WaitAsync();
        try
        {
            _disposed = true;
            StopProcess();
        }
        finally { _startGate.Release(); }
    }
}

internal sealed record SidecarIceServer(string[] Urls, string? Username, string? Credential);

internal sealed record SidecarHello(string Token, string Role, SidecarIceServer[] IceServers, bool RelayOnly, int MaxMessageBytes,
    int MinCwnd, bool AllowLoopback, int? TurnFlows = null);

internal sealed record SidecarDescription(string Type, string Sdp);

internal sealed record SidecarCandidate(string Candidate, string? SdpMid, int? SdpMLineIndex);

internal sealed record SidecarPath(string Local, string LocalProtocol, string? RelayProtocol, string Remote, double RttMs);

internal sealed record SidecarState(string Ice, string Peer, string Channel, SidecarPath? Path);

internal sealed record SidecarOffer(bool IceRestart);

internal sealed record SidecarError(string Message);

[System.Text.Json.Serialization.JsonSourceGenerationOptions(PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
[System.Text.Json.Serialization.JsonSerializable(typeof(SidecarHello))]
[System.Text.Json.Serialization.JsonSerializable(typeof(SidecarDescription))]
[System.Text.Json.Serialization.JsonSerializable(typeof(SidecarCandidate))]
[System.Text.Json.Serialization.JsonSerializable(typeof(SidecarState))]
[System.Text.Json.Serialization.JsonSerializable(typeof(SidecarOffer))]
[System.Text.Json.Serialization.JsonSerializable(typeof(SidecarError))]
internal sealed partial class SidecarJson : System.Text.Json.Serialization.JsonSerializerContext;

internal static class SidecarMessage
{
    public const byte Hello = 0x01, Sdp = 0x02, Candidate = 0x03, State = 0x04, Data = 0x05, Buffered = 0x06,
        CreateOffer = 0x07, Error = 0x08, Close = 0x09;

    public static byte[] Frame(byte kind, ReadOnlySpan<byte> payload)
    {
        var frame = new byte[5 + payload.Length];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(frame, (uint)(1 + payload.Length));
        frame[4] = kind;
        payload.CopyTo(frame.AsSpan(5));
        return frame;
    }

    public static string Utf8(ReadOnlySpan<byte> payload) => Encoding.UTF8.GetString(payload);
}
