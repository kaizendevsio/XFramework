using System.Collections.Concurrent;
using System.Security.Claims;
using Bolt.Protocol.Transport;
using Bolt.Server;

namespace Bolt.Tests;

/// <summary>
/// An in-memory stand-in for ICE/DTLS/SCTP: peers created by the relay's factory and by a participant's are
/// paired by their SDP, "connect" once both descriptions are applied (unless the network is unreachable, as
/// with UDP blocked), and carry messages to each other with optional loss. No sockets are opened.
/// </summary>
internal sealed class FakeRtcNetwork
{
    private readonly ConcurrentDictionary<string, FakeRtcPeer> _offers = new();
    public bool Reachable { get; set; } = true;
    /// <summary>Drop every Nth message in each direction (0 = none).</summary>
    public int DropEvery { get; set; }
    public RtcPath Path { get; set; } = new("relay", "udp", "udp", "srflx", 120);
    public ConcurrentQueue<FakeRtcPeer> Created { get; } = new();

    public IRtcPeerFactory Factory(RtcPeerRole expected) => new PeerFactory(this, expected);

    public FakeRtcPeer Create(RtcPeerRole role, RtcPeerOptions options)
    {
        var peer = new FakeRtcPeer(this, role, options);
        Created.Enqueue(peer);
        return peer;
    }

    internal void RegisterOffer(string offer, FakeRtcPeer peer) => _offers[offer] = peer;
    internal FakeRtcPeer? FindOffer(string offer) => _offers.GetValueOrDefault(offer);

    internal void TryConnect(FakeRtcPeer a, FakeRtcPeer b)
    {
        if (!a.HasRemote || !b.HasRemote || !Reachable) return;
        a.Remote = b;
        b.Remote = a;
        _ = Task.Run(async () =>
        {
            await Task.Delay(20);
            if (!Reachable) return;
            a.Open(Path);
            b.Open(Path with { Local = "relay", Remote = "relay" });
        });
    }

    private sealed class PeerFactory(FakeRtcNetwork network, RtcPeerRole expected) : IRtcPeerFactory
    {
        public ValueTask<IRtcPeer> CreateAsync(RtcPeerRole role, RtcPeerOptions options, CancellationToken ct)
        {
            if (role != expected) throw new InvalidOperationException("Unexpected peer role.");
            return ValueTask.FromResult<IRtcPeer>(network.Create(role, options));
        }
    }
}

internal sealed class FakeRtcPeer(FakeRtcNetwork network, RtcPeerRole role, RtcPeerOptions options) : IRtcPeer
{
    private int _state = (int)RtcChannelState.Connecting;
    private int _sent;
    public RtcPeerOptions Options { get; } = options;
    public RtcPeerRole Role { get; } = role;
    public RtcChannelState State => (RtcChannelState)Volatile.Read(ref _state);
    public RtcPath? Path { get; private set; }
    public int MaxMessageBytes => Options.MaxMessageBytes;
    public long BufferedAmount { get; set; }
    public long CongestionWindow { get; set; }
    public long Dropped { get; private set; }
    public bool Refuse { get; set; }
    public bool HasRemote { get; private set; }
    public FakeRtcPeer? Remote { get; set; }
    /// <summary>The peer on the other side of the offer/answer, before the network connects them.</summary>
    public FakeRtcPeer? Partner { get; set; }
    public string? Offer { get; private set; }
    public int Offers { get; private set; }
    public int IceRestarts { get; private set; }
    public bool Disposed { get; private set; }
    public ConcurrentQueue<byte[]> Sent { get; } = new();
    public ConcurrentQueue<RtcCandidate> RemoteCandidates { get; } = new();

    public event Action<RtcCandidate>? LocalCandidate;
    public event Action<RtcChannelState>? StateChanged;
    public event Action<ReadOnlyMemory<byte>>? Message;
    public event Action? BufferedAmountLow;
    public event Action<RtcPath>? PathChanged;

    public Task<string> CreateOfferAsync(bool iceRestart, CancellationToken ct)
    {
        Offers++;
        if (iceRestart) IceRestarts++;
        Offer ??= "offer:" + Guid.NewGuid().ToString("N");
        network.RegisterOffer(Offer, this);
        _ = Task.Run(() => { LocalCandidate?.Invoke(new RtcCandidate("candidate:1 1 udp 1 192.0.2.1 50000 typ host", "0", 0)); LocalCandidate?.Invoke(new RtcCandidate("", null, null)); });
        return Task.FromResult(Offer);
    }

    public Task<string> AnswerAsync(string offerSdp, CancellationToken ct)
    {
        var offerer = network.FindOffer(offerSdp) ?? throw new InvalidOperationException("Unknown offer.");
        HasRemote = true;
        Partner = offerer;
        offerer.Partner = this;
        _ = Task.Run(() => { LocalCandidate?.Invoke(new RtcCandidate("candidate:2 1 udp 1 198.51.100.7 3478 typ relay", "0", 0)); LocalCandidate?.Invoke(new RtcCandidate("", null, null)); });
        network.TryConnect(this, offerer);
        return Task.FromResult("answer:" + offerSdp);
    }

    public Task SetAnswerAsync(string answerSdp, CancellationToken ct)
    {
        HasRemote = true;
        // An ICE restart on an open pair keeps it open; a first answer connects the pair.
        if (Partner is { } partner && Remote is null) network.TryConnect(partner, this);
        return Task.CompletedTask;
    }

    public Task AddCandidateAsync(RtcCandidate candidate, CancellationToken ct)
    {
        RemoteCandidates.Enqueue(candidate);
        return Task.CompletedTask;
    }

    public bool TrySend(ReadOnlySpan<byte> message)
    {
        if (State != RtcChannelState.Open || Refuse || message.Length > MaxMessageBytes) { Dropped++; return false; }
        var copy = message.ToArray();
        Sent.Enqueue(copy);
        var count = Interlocked.Increment(ref _sent);
        if (network.DropEvery > 0 && count % network.DropEvery == 0) return true;
        Remote?.Deliver(copy);
        return true;
    }

    public void Deliver(byte[] message)
    {
        if (State == RtcChannelState.Open) Message?.Invoke(message);
    }

    public void Open(RtcPath path)
    {
        Path = path;
        SetState(RtcChannelState.Open);
        PathChanged?.Invoke(path);
    }

    public void Fail() => SetState(RtcChannelState.Failed);

    public void Drain(long buffered)
    {
        BufferedAmount = buffered;
        BufferedAmountLow?.Invoke();
    }

    private void SetState(RtcChannelState state)
    {
        var previous = (RtcChannelState)Interlocked.Exchange(ref _state, (int)state);
        if (previous is RtcChannelState.Closed or RtcChannelState.Failed) { Interlocked.Exchange(ref _state, (int)previous); return; }
        if (previous != state) StateChanged?.Invoke(state);
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        SetState(RtcChannelState.Closed);
        return ValueTask.CompletedTask;
    }
}

/// <summary>Grants distinct client and relay credentials and records who asked.</summary>
internal sealed class FakeIceSource : IBoltIceServerSource
{
    public static readonly RtcIceServer[] ClientServers = [new(["turn:turn.example.test:3478?transport=udp"], "client-user", "client-secret")];
    public static readonly RtcIceServer[] RelayServers = [new(["turn:turn.example.test:3478?transport=udp"], "relay-user", "relay-secret")];
    public ConcurrentQueue<string?> Requests { get; } = new();
    public bool Unavailable { get; set; }
    public bool Throws { get; set; }
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromHours(1);

    public ValueTask<BoltIceGrant?> GrantAsync(ClaimsPrincipal participant, CancellationToken ct)
    {
        Requests.Enqueue(participant.FindFirstValue("bolt_media_client_id"));
        if (Throws) throw new HttpRequestException("provider down");
        return ValueTask.FromResult(Unavailable ? null : new BoltIceGrant(ClientServers, RelayServers, DateTimeOffset.UtcNow + Lifetime));
    }
}
