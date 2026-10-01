using System.Buffers;
using System.Buffers.Binary;
using System.Text.Json;
using System.Threading.Channels;
using Bolt.Protocol.Transport;
using Microsoft.Extensions.Logging;

namespace Bolt.Rtc;

/// <summary>
/// One peer connection living in the bolt-rtc sidecar, driven over its own local socket. Control messages
/// (SDP, candidates) are written in order; media goes through a bounded queue drained by one writer, so
/// <see cref="TrySend"/> never waits. <see cref="BufferedAmount"/> is what the sidecar reported for the data
/// channel plus what is still on its way to it.
/// </summary>
internal sealed class SidecarRtcPeer : IRtcPeer
{
    /// <summary>Media not yet written to the local socket. Beyond this a send is refused, never queued.</summary>
    private const int MaxLocalPendingBytes = 512 * 1024;

    private readonly Stream _stream;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _controlGate = new(1, 1);
    private readonly Channel<byte[]> _outbound = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _closed = new();
    private TaskCompletionSource<string>? _pendingDescription;
    private Task _readLoop = Task.CompletedTask;
    private Task _writeLoop = Task.CompletedTask;
    private long _localPending;
    private long _reportedBuffered;
    private long _cwnd;
    private long _dropped;
    private long _sidecarDropped;
    private int _state = (int)RtcChannelState.Connecting;
    private RtcPath? _path;
    private int _disposed;

    public SidecarRtcPeer(Stream stream, RtcPeerRole role, int maxMessageBytes, ILogger logger)
    {
        _stream = stream;
        Role = role;
        MaxMessageBytes = maxMessageBytes;
        _logger = logger;
    }

    public RtcPeerRole Role { get; }
    public RtcChannelState State => (RtcChannelState)Volatile.Read(ref _state);
    public RtcPath? Path => Volatile.Read(ref _path);
    public int MaxMessageBytes { get; }
    public long BufferedAmount => Interlocked.Read(ref _reportedBuffered) + Interlocked.Read(ref _localPending);
    public long CongestionWindow => Interlocked.Read(ref _cwnd);
    public long Dropped => Interlocked.Read(ref _dropped) + Interlocked.Read(ref _sidecarDropped);

    public event Action<RtcCandidate>? LocalCandidate;
    public event Action<RtcChannelState>? StateChanged;
    public event Action<ReadOnlyMemory<byte>>? Message;
    public event Action? BufferedAmountLow;
    public event Action<RtcPath>? PathChanged;

    internal async Task StartAsync(byte[] hello, CancellationToken ct)
    {
        await WriteControlAsync(SidecarMessage.Hello, hello, ct);
        _readLoop = Task.Run(ReadLoopAsync);
        _writeLoop = Task.Run(WriteLoopAsync);
    }

    public async Task<string> CreateOfferAsync(bool iceRestart, CancellationToken ct)
    {
        if (Role != RtcPeerRole.Offer) throw new InvalidOperationException("Only the offering side creates offers.");
        return await ExchangeAsync(SidecarMessage.CreateOffer,
            JsonSerializer.SerializeToUtf8Bytes(new SidecarOffer(iceRestart), SidecarJson.Default.SidecarOffer), ct);
    }

    public async Task<string> AnswerAsync(string offerSdp, CancellationToken ct)
    {
        if (Role != RtcPeerRole.Answer) throw new InvalidOperationException("Only the answering side answers.");
        return await ExchangeAsync(SidecarMessage.Sdp,
            JsonSerializer.SerializeToUtf8Bytes(new SidecarDescription("offer", offerSdp), SidecarJson.Default.SidecarDescription), ct);
    }

    public Task SetAnswerAsync(string answerSdp, CancellationToken ct)
    {
        if (Role != RtcPeerRole.Offer) throw new InvalidOperationException("Only the offering side takes an answer.");
        return WriteControlAsync(SidecarMessage.Sdp,
            JsonSerializer.SerializeToUtf8Bytes(new SidecarDescription("answer", answerSdp), SidecarJson.Default.SidecarDescription), ct);
    }

    public Task AddCandidateAsync(RtcCandidate candidate, CancellationToken ct) =>
        WriteControlAsync(SidecarMessage.Candidate, JsonSerializer.SerializeToUtf8Bytes(
            new SidecarCandidate(candidate.Candidate, candidate.SdpMid, candidate.SdpMLineIndex), SidecarJson.Default.SidecarCandidate), ct);

    public bool TrySend(ReadOnlySpan<byte> message)
    {
        if (State != RtcChannelState.Open || message.IsEmpty || message.Length > MaxMessageBytes ||
            Interlocked.Read(ref _localPending) + message.Length > MaxLocalPendingBytes)
        {
            Interlocked.Increment(ref _dropped);
            return false;
        }
        var frame = SidecarMessage.Frame(SidecarMessage.Data, message);
        Interlocked.Add(ref _localPending, message.Length);
        if (_outbound.Writer.TryWrite(frame)) return true;
        Interlocked.Add(ref _localPending, -message.Length);
        Interlocked.Increment(ref _dropped);
        return false;
    }

    private async Task<string> ExchangeAsync(byte kind, byte[] payload, CancellationToken ct)
    {
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (Interlocked.CompareExchange(ref _pendingDescription, pending, null) is not null)
            throw new InvalidOperationException("A description exchange is already in progress.");
        try
        {
            await WriteControlAsync(kind, payload, ct);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _closed.Token);
            return await pending.Task.WaitAsync(TimeSpan.FromSeconds(15), linked.Token);
        }
        finally { Interlocked.CompareExchange(ref _pendingDescription, null, pending); }
    }

    private async Task WriteControlAsync(byte kind, byte[] payload, CancellationToken ct)
    {
        var frame = SidecarMessage.Frame(kind, payload);
        await _controlGate.WaitAsync(ct);
        try
        {
            if (_closed.IsCancellationRequested) throw new InvalidOperationException("The peer is closed.");
            await _stream.WriteAsync(frame, ct);
            await _stream.FlushAsync(ct);
        }
        catch (IOException ex) { Fail(ex); throw new InvalidOperationException("The WebRTC sidecar connection was lost.", ex); }
        finally { _controlGate.Release(); }
    }

    private async Task WriteLoopAsync()
    {
        var ct = _closed.Token;
        try
        {
            while (await _outbound.Reader.WaitToReadAsync(ct))
            {
                await _controlGate.WaitAsync(ct);
                try
                {
                    // Write whatever is queued in one go, then let control messages in.
                    var batch = 0;
                    while (batch < 64 && _outbound.Reader.TryRead(out var frame))
                    {
                        await _stream.WriteAsync(frame, ct);
                        Interlocked.Add(ref _localPending, -(frame.Length - 5));
                        batch++;
                    }
                    await _stream.FlushAsync(ct);
                }
                finally { _controlGate.Release(); }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Fail(ex); }
    }

    private async Task ReadLoopAsync()
    {
        var header = new byte[4];
        var buffer = ArrayPool<byte>.Shared.Rent(70 * 1024);
        try
        {
            while (!_closed.IsCancellationRequested)
            {
                await _stream.ReadExactlyAsync(header, _closed.Token);
                var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(header);
                if (length is <= 0 or > 65 * 1024) throw new InvalidDataException("Invalid sidecar frame.");
                await _stream.ReadExactlyAsync(buffer.AsMemory(0, length), _closed.Token);
                Handle(buffer[0], buffer.AsMemory(1, length - 1));
            }
        }
        catch (OperationCanceledException) { }
        catch (EndOfStreamException) { Fail(null); }
        catch (Exception ex) { Fail(ex); }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    private void Handle(byte kind, ReadOnlyMemory<byte> payload)
    {
        switch (kind)
        {
            case SidecarMessage.Data:
                try { Message?.Invoke(payload); }
                catch (Exception ex) { _logger.LogDebug(ex, "A data channel message handler failed"); }
                break;
            case SidecarMessage.Buffered when payload.Length >= 16:
            {
                var span = payload.Span;
                var before = Interlocked.Exchange(ref _reportedBuffered, BinaryPrimitives.ReadUInt32LittleEndian(span));
                Interlocked.Exchange(ref _cwnd, BinaryPrimitives.ReadUInt32LittleEndian(span[4..]));
                Interlocked.Exchange(ref _sidecarDropped, BinaryPrimitives.ReadUInt32LittleEndian(span[12..]));
                if (Interlocked.Read(ref _reportedBuffered) < before) BufferedAmountLow?.Invoke();
                break;
            }
            case SidecarMessage.Sdp:
            {
                var description = JsonSerializer.Deserialize(payload.Span, SidecarJson.Default.SidecarDescription);
                if (description is not null) Volatile.Read(ref _pendingDescription)?.TrySetResult(description.Sdp);
                break;
            }
            case SidecarMessage.Candidate:
            {
                var candidate = JsonSerializer.Deserialize(payload.Span, SidecarJson.Default.SidecarCandidate);
                if (candidate is not null) LocalCandidate?.Invoke(new RtcCandidate(candidate.Candidate, candidate.SdpMid, candidate.SdpMLineIndex));
                break;
            }
            case SidecarMessage.State:
            {
                var state = JsonSerializer.Deserialize(payload.Span, SidecarJson.Default.SidecarState);
                if (state is null) break;
                if (state.Path is { } path)
                {
                    var current = new RtcPath(path.Local, path.LocalProtocol, path.RelayProtocol, path.Remote, path.RttMs);
                    if (current != Volatile.Read(ref _path))
                    {
                        Volatile.Write(ref _path, current);
                        PathChanged?.Invoke(current);
                    }
                }
                SetState(Map(state));
                break;
            }
            case SidecarMessage.Error:
            {
                var error = JsonSerializer.Deserialize(payload.Span, SidecarJson.Default.SidecarError);
                var message = error?.Message ?? "unknown";
                _logger.LogDebug("WebRTC sidecar refused a request: {Message}", message);
                Volatile.Read(ref _pendingDescription)?.TrySetException(new InvalidOperationException("WebRTC negotiation failed: " + message));
                break;
            }
        }
    }

    private RtcChannelState Map(SidecarState state) =>
        state.Channel == "open" && state.Peer is not ("failed" or "closed") ? RtcChannelState.Open
        : state.Peer is "failed" || state.Ice is "failed" ? RtcChannelState.Failed
        : state.Peer is "closed" || state.Channel is "closed" ? RtcChannelState.Closed
        : State == RtcChannelState.Open ? RtcChannelState.Open : RtcChannelState.Connecting;

    private void SetState(RtcChannelState next)
    {
        var previous = (RtcChannelState)Interlocked.Exchange(ref _state, (int)next);
        // Closed and failed are final for this peer.
        if (previous is RtcChannelState.Closed or RtcChannelState.Failed)
        {
            Interlocked.Exchange(ref _state, (int)previous);
            return;
        }
        if (previous != next) StateChanged?.Invoke(next);
    }

    private void Fail(Exception? ex)
    {
        if (ex is not null && !_closed.IsCancellationRequested)
            _logger.LogDebug(ex, "WebRTC sidecar peer connection ended");
        SetState(State == RtcChannelState.Open ? RtcChannelState.Closed : RtcChannelState.Failed);
        Volatile.Read(ref _pendingDescription)?.TrySetException(new InvalidOperationException("The WebRTC peer closed."));
        try { _closed.Cancel(); } catch (ObjectDisposedException) { }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await WriteControlAsync(SidecarMessage.Close, [], timeout.Token);
        }
        catch { /* The sidecar closes the peer when the socket closes anyway. */ }
        SetState(RtcChannelState.Closed);
        try { _closed.Cancel(); } catch (ObjectDisposedException) { }
        _outbound.Writer.TryComplete();
        await _stream.DisposeAsync();
        try { await Task.WhenAll(_readLoop, _writeLoop).WaitAsync(TimeSpan.FromSeconds(2)); } catch { /* Shutdown. */ }
        _closed.Dispose();
        _controlGate.Dispose();
    }
}
