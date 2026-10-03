using Bolt.Protocol.Transport;

namespace Bolt.Media.Browser;

/// <summary>Creates browser WebRTC peers (bolt-rtc.js) for the datagram media path.</summary>
public sealed class BoltRtcInterop(IJSRuntime js, ILogger<BoltRtcInterop> logger) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    private async ValueTask<IJSObjectReference> ModuleAsync() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./_content/Bolt.Media.Browser/bolt-rtc.js");

    /// <summary>False where the browser has no RTCPeerConnection: the call simply stays on its WebSocket.</summary>
    public async ValueTask<bool> IsSupportedAsync()
    {
        try { return await (await ModuleAsync()).InvokeAsync<bool>("isSupported"); }
        catch (JSException) { return false; }
    }

    public async ValueTask<IRtcPeer> CreatePeerAsync(RtcPeerOptions options, CancellationToken ct)
    {
        var module = await ModuleAsync();
        var peer = new BrowserRtcPeer(options.MaxMessageBytes, logger);
        try
        {
            await peer.StartAsync(module, options, ct);
            return peer;
        }
        catch
        {
            await peer.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try { await _module.DisposeAsync(); }
            catch (JSDisconnectedException) { }
        }
    }
}

/// <summary>
/// An <see cref="IRtcPeer"/> backed by the browser's RTCPeerConnection. Sends and the buffered amount are
/// synchronous calls into the page (the sender's pacer makes one per frame); everything else is async.
/// </summary>
public sealed class BrowserRtcPeer : IRtcPeer
{
    private readonly ILogger _logger;
    private IJSObjectReference? _peer;
    private DotNetObjectReference<BrowserRtcPeer>? _self;
    private int _state = (int)RtcChannelState.Connecting;
    private RtcPath? _path;
    private long _dropped;
    private bool _disposed;

    internal BrowserRtcPeer(int maxMessageBytes, ILogger logger)
    {
        MaxMessageBytes = maxMessageBytes;
        _logger = logger;
    }

    public RtcPeerRole Role => RtcPeerRole.Offer;
    public RtcChannelState State => (RtcChannelState)Volatile.Read(ref _state);
    public RtcPath? Path => Volatile.Read(ref _path);
    public int MaxMessageBytes { get; }
    public long CongestionWindow => 0;
    public long Dropped => Interlocked.Read(ref _dropped);

    public long BufferedAmount
    {
        get
        {
            if (_disposed || _peer is not IJSInProcessObjectReference local) return 0;
            try { return (long)local.Invoke<double>("bufferedAmount"); }
            catch (JSException) { return 0; }
        }
    }

    public event Action<RtcCandidate>? LocalCandidate;
    public event Action<RtcChannelState>? StateChanged;
    public event Action<ReadOnlyMemory<byte>>? Message;
    public event Action? BufferedAmountLow;
    public event Action<RtcPath>? PathChanged;

    internal async Task StartAsync(IJSObjectReference module, RtcPeerOptions options, CancellationToken ct)
    {
        _self = DotNetObjectReference.Create(this);
        _peer = await module.InvokeAsync<IJSObjectReference>("createPeer", ct, _self, new
        {
            iceServers = options.IceServers.Select(x => new { urls = x.Urls, username = x.Username, credential = x.Credential }).ToArray(),
            iceTransportPolicy = options.RelayOnly ? "relay" : "all",
            maxMessageBytes = options.MaxMessageBytes,
        });
    }

    public async Task<string> CreateOfferAsync(bool iceRestart, CancellationToken ct) =>
        await Require().InvokeAsync<string>("createOffer", ct, iceRestart);

    public Task<string> AnswerAsync(string offerSdp, CancellationToken ct) =>
        throw new NotSupportedException("The browser always offers.");

    public async Task SetAnswerAsync(string answerSdp, CancellationToken ct) =>
        await Require().InvokeVoidAsync("setAnswer", ct, answerSdp);

    public async Task AddCandidateAsync(RtcCandidate candidate, CancellationToken ct) =>
        await Require().InvokeVoidAsync("addCandidate", ct, candidate.Candidate, candidate.SdpMid, candidate.SdpMLineIndex);

    public bool TrySend(ReadOnlySpan<byte> message)
    {
        if (State != RtcChannelState.Open || message.Length > MaxMessageBytes || _peer is not IJSInProcessObjectReference local)
        {
            Interlocked.Increment(ref _dropped);
            return false;
        }
        try
        {
            if (local.Invoke<bool>("send", message.ToArray())) return true;
        }
        catch (JSException ex) { _logger.LogDebug(ex, "A data channel send failed"); }
        Interlocked.Increment(ref _dropped);
        return false;
    }

    private IJSObjectReference Require() => _peer ?? throw new ObjectDisposedException(nameof(BrowserRtcPeer));

    [JSInvokable]
    public void OnMessage(byte[] data)
    {
        try { Message?.Invoke(data); }
        catch (Exception ex) { _logger.LogDebug(ex, "A data channel message handler failed"); }
    }

    [JSInvokable]
    public void OnCandidate(string candidate, string? sdpMid, int? sdpMLineIndex) =>
        LocalCandidate?.Invoke(new RtcCandidate(candidate, sdpMid, sdpMLineIndex));

    [JSInvokable]
    public void OnState(string state)
    {
        var next = state switch
        {
            "open" => RtcChannelState.Open,
            // The channel is open but ICE has gone quiet or is checking again: media takes the WebSocket meanwhile.
            "stalled" => RtcChannelState.Stalled,
            "closed" => RtcChannelState.Closed,
            "failed" => RtcChannelState.Failed,
            _ => RtcChannelState.Connecting,
        };
        var previous = (RtcChannelState)Interlocked.Exchange(ref _state, (int)next);
        if (previous is RtcChannelState.Closed or RtcChannelState.Failed)
        {
            Interlocked.Exchange(ref _state, (int)previous);
            return;
        }
        if (previous != next) StateChanged?.Invoke(next);
    }

    [JSInvokable]
    public void OnPath(string local, string localProtocol, string? relayProtocol, string remote, double rttMs)
    {
        var path = new RtcPath(local, localProtocol, relayProtocol, remote, rttMs);
        Volatile.Write(ref _path, path);
        PathChanged?.Invoke(path);
    }

    [JSInvokable]
    public void OnBufferedLow() => BufferedAmountLow?.Invoke();

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        OnState("closed");
        if (_peer is not null)
        {
            try { await _peer.InvokeVoidAsync("close"); await _peer.DisposeAsync(); }
            catch (JSException) { }
            catch (JSDisconnectedException) { }
            _peer = null;
        }
        _self?.Dispose();
    }
}
