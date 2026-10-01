using System.Buffers;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading.Channels;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using Microsoft.Extensions.Logging;

namespace Bolt.Server;

/// <summary>
/// Datagram media paths. A participant of an authenticated call may ask, over its WebSocket, for a WebRTC
/// data channel to the relay. Signalling (offer, answer, candidates) rides the same authenticated socket,
/// and the channel is bound to that one connection: whatever arrives on it is processed exactly as if it
/// had arrived on the socket, from the same registered participant, but only media and media feedback
/// are accepted that way. The relay's per-receiver lanes decide what is sent and dropped as before; the
/// channel only replaces the pipe under them, and the WebSocket takes over again the moment the channel
/// is gone. Frames are SFrame-encrypted end to end before they reach either pipe.
/// </summary>
public sealed partial class BoltServer
{
    private readonly BoltMediaTransportOptions? _mediaTransport;
    private readonly ConcurrentDictionary<string, ConnectionMediaTransport> _mediaTransports = new(StringComparer.Ordinal);
    private Timer? _mediaTransportTimer;

    /// <summary>Most signalling messages waiting for one connection's worker; more are dropped.</summary>
    private const int MaxQueuedTransportMessages = 64;
    private const int MaxCandidatesPerSession = 64;

    internal sealed class ConnectionMediaTransport(BoltHubConnection connection)
    {
        public object Sync { get; } = new();
        public BoltHubConnection Connection { get; } = connection;
        public Channel<byte[]> Inbox { get; } = Channel.CreateBounded<byte[]>(
            new BoundedChannelOptions(MaxQueuedTransportMessages) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });
        public Dictionary<string, TransportSession> Sessions { get; } = new(StringComparer.Ordinal);
        /// <summary>A worker is draining <see cref="Inbox"/>. Changed under <see cref="Sync"/> only.</summary>
        public bool Running;
        public long LastRequestTick = long.MinValue / 2;
        public int Requests;
        public string? ActiveSession;
        public long LossQuietSince;
        public bool Closed;
    }

    internal sealed class TransportSession(string id, BoltIceGrant grant)
    {
        public string Id { get; } = id;
        public BoltIceGrant Grant { get; } = grant;
        public IRtcPeer? Peer { get; set; }
        public int Candidates;
        public bool Renewing;
    }

    /// <summary>Whether this relay offers datagram paths at all (a host with TURN credentials and the sidecar).</summary>
    public bool DatagramTransportEnabled => _mediaTransport is not null;

    private void StartMediaTransportTimer()
    {
        if (_mediaTransport is null) return;
        _mediaTransportTimer = new Timer(_ => _ = MediaTransportTickAsync(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    /// <summary>Signalling never runs on the connection's receive loop: one worker per connection, in order.</summary>
    private void EnqueueMediaTransport(BoltHubConnection connection, byte[] buffer, int length)
    {
        if (!MediaTransportCodec.TryRead(buffer.AsSpan(0, length), out _, out _))
            return;
        var transport = _mediaTransports.GetOrAdd(connection.StreamId, _ => new ConnectionMediaTransport(connection));
        if (!ReferenceEquals(transport.Connection, connection))
            return;
        // Written first, then a worker started if none runs: the worker only stops after finding the inbox
        // empty under the same lock, so a message can never be left behind.
        transport.Inbox.Writer.TryWrite(buffer.AsSpan(0, length).ToArray());
        lock (transport.Sync)
        {
            if (transport.Closed || transport.Running) return;
            transport.Running = true;
        }
        _ = Task.Run(() => RunMediaTransportWorkerAsync(transport));
    }

    private async Task RunMediaTransportWorkerAsync(ConnectionMediaTransport transport)
    {
        var ct = _shutdownCts.Token;
        try
        {
            while (true)
            {
                while (transport.Inbox.Reader.TryRead(out var frame))
                {
                    if (transport.Closed || !transport.Connection.IsAlive || ct.IsCancellationRequested) return;
                    try { await HandleMediaTransportAsync(transport, frame, ct); }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                    catch (Exception ex)
                    {
                        // Never fatal for the call: the participant keeps its WebSocket path.
                        _logger.LogDebug(ex, "Media transport signalling failed for {ClientId}", transport.Connection.ClientId);
                    }
                }
                lock (transport.Sync)
                {
                    if (transport.Inbox.Reader.Count == 0)
                    {
                        transport.Running = false;
                        return;
                    }
                }
            }
        }
        catch
        {
            lock (transport.Sync) transport.Running = false;
            throw;
        }
    }

    private async Task HandleMediaTransportAsync(ConnectionMediaTransport transport, byte[] frame, CancellationToken ct)
    {
        if (!MediaTransportCodec.TryRead(frame, out var kind, out var payloadSpan))
            return;
        var payload = payloadSpan.ToArray();
        var connection = transport.Connection;
        switch (kind)
        {
            case MediaTransportKind.Request:
                await HandleTransportRequestAsync(transport, ct);
                break;
            case MediaTransportKind.Offer:
            {
                if (MediaTransportCodec.Decode<MediaTransportDescription>(payload) is not { } offer || offer.Sdp.Length > MediaTransportCodec.MaxPayloadBytes)
                    return;
                var session = FindSession(transport, offer.Session);
                if (session is null || _mediaTransport is not { } options)
                    return;
                if (session.Peer is null)
                {
                    var peer = await options.Peers.CreateAsync(RtcPeerRole.Answer,
                        new RtcPeerOptions(session.Grant.Server, options.RelayOnly, options.MaxMessageBytes, options.MinCwndBytes, AllowLoopback), ct);
                    if (!AttachSessionPeer(transport, session, peer))
                    {
                        await peer.DisposeAsync();
                        return;
                    }
                }
                var answer = await session.Peer!.AnswerAsync(offer.Sdp, ct);
                await SendTransportAsync(connection, MediaTransportKind.Answer, new MediaTransportDescription(session.Id, answer, offer.IceRestart));
                break;
            }
            case MediaTransportKind.Candidate:
            {
                if (MediaTransportCodec.Decode<MediaTransportCandidate>(payload) is not { } candidate)
                    return;
                var session = FindSession(transport, candidate.Session);
                if (session?.Peer is not { } peer || Interlocked.Increment(ref session.Candidates) > MaxCandidatesPerSession)
                    return;
                await peer.AddCandidateAsync(new RtcCandidate(candidate.Candidate, candidate.SdpMid, candidate.SdpMLineIndex), ct);
                break;
            }
            case MediaTransportKind.Report:
            {
                if (MediaTransportCodec.Decode<MediaTransportReport>(payload) is { } report && report.Session == transport.ActiveSession)
                    ApplyReceiverLoss(transport, Math.Clamp(report.ReceiveLossPermille, 0, 1000));
                break;
            }
            case MediaTransportKind.Close:
            {
                if (MediaTransportCodec.Decode<MediaTransportClose>(payload) is { } close && FindSession(transport, close.Session) is { } session)
                    await CloseSessionAsync(transport, session, notify: false);
                break;
            }
        }
    }

    private async Task HandleTransportRequestAsync(ConnectionMediaTransport transport, CancellationToken ct)
    {
        var connection = transport.Connection;
        var now = Environment.TickCount64;
        if (_mediaTransport is not { } options)
        {
            await SendTransportAsync(connection, MediaTransportKind.Config, Unavailable("disabled"));
            return;
        }
        if (now - transport.LastRequestTick < options.RequestSpacingSeconds * 1000L || transport.Requests >= options.MaxRequestsPerConnection)
        {
            await SendTransportAsync(connection, MediaTransportKind.Config, Unavailable("rate-limited"));
            return;
        }
        transport.LastRequestTick = now;
        transport.Requests++;
        if (await StartSessionAsync(transport, ct) is null)
            await SendTransportAsync(connection, MediaTransportKind.Config, Unavailable("unavailable"));
    }

    /// <summary>Mint credentials and announce a new session (also used to renew one). Null when there are none.</summary>
    private async Task<TransportSession?> StartSessionAsync(ConnectionMediaTransport transport, CancellationToken ct)
    {
        var options = _mediaTransport!;
        if (transport.Connection.User is not { } user)
            return null;
        BoltIceGrant? grant;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            grant = await options.IceServers.GrantAsync(user, timeout.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // The provider's message may name the endpoint; never the token, which it never sees here.
            _logger.LogWarning("ICE credentials could not be issued ({Error}); media stays on the WebSocket", ex.GetType().Name);
            return null;
        }
        if (grant is null || grant.Client.Count == 0 || grant.Server.Count == 0)
            return null;
        var session = new TransportSession(Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant(), grant);
        TransportSession? evicted = null;
        lock (transport.Sync)
        {
            if (transport.Closed) return null;
            // At most the active session and one being set up: a third request replaces the pending one.
            if (transport.Sessions.Count >= 2)
                evicted = transport.Sessions.Values.FirstOrDefault(x => x.Id != transport.ActiveSession);
            if (evicted is not null) transport.Sessions.Remove(evicted.Id);
            transport.Sessions[session.Id] = session;
        }
        if (evicted is not null) await CloseSessionAsync(transport, evicted, notify: true);
        await SendTransportAsync(transport.Connection, MediaTransportKind.Config, new MediaTransportConfig(
            session.Id, grant.Client.ToArray(), "all", options.MaxMessageBytes, grant.ExpiresAt.ToUnixTimeSeconds()));
        return session;
    }

    private static MediaTransportConfig Unavailable(string reason) => new("", [], "all", 0, 0, reason);

    private static TransportSession? FindSession(ConnectionMediaTransport transport, string id)
    {
        lock (transport.Sync) return transport.Sessions.GetValueOrDefault(id);
    }

    private bool AttachSessionPeer(ConnectionMediaTransport transport, TransportSession session, IRtcPeer peer)
    {
        lock (transport.Sync)
        {
            if (transport.Closed || !transport.Sessions.ContainsKey(session.Id)) return false;
            session.Peer = peer;
        }
        var connection = transport.Connection;
        peer.LocalCandidate += candidate => _ = SendTransportAsync(connection, MediaTransportKind.Candidate,
            new MediaTransportCandidate(session.Id, candidate.Candidate, candidate.SdpMid, candidate.SdpMLineIndex));
        peer.StateChanged += state => _ = OnSessionStateAsync(transport, session, state);
        peer.PathChanged += path =>
        {
            if (peer.State == RtcChannelState.Open)
                _ = SendTransportAsync(connection, MediaTransportKind.State, new MediaTransportStateMessage(session.Id, "open", path));
        };
        peer.Message += data => OnDatagram(connection, data);
        return true;
    }

    private async Task OnSessionStateAsync(ConnectionMediaTransport transport, TransportSession session, RtcChannelState state)
    {
        var connection = transport.Connection;
        if (state == RtcChannelState.Open && session.Peer is { } peer)
        {
            TransportSession[] superseded;
            lock (transport.Sync)
            {
                if (transport.Closed || !transport.Sessions.ContainsKey(session.Id)) return;
                transport.ActiveSession = session.Id;
                superseded = transport.Sessions.Values.Where(x => x.Id != session.Id).ToArray();
                foreach (var old in superseded) transport.Sessions.Remove(old.Id);
            }
            connection.AttachDatagram(peer);
            _logger.LogInformation("Datagram media path open for {ClientId} via {Path}", connection.ClientId, peer.Path?.Describe() ?? "unknown");
            await SendTransportAsync(connection, MediaTransportKind.State, new MediaTransportStateMessage(session.Id, "open", peer.Path));
            foreach (var old in superseded) await CloseSessionAsync(transport, old, notify: true);
            return;
        }
        if (state is RtcChannelState.Closed or RtcChannelState.Failed)
        {
            bool removed;
            lock (transport.Sync) removed = transport.Sessions.Remove(session.Id);
            if (!removed) return;
            await CloseSessionAsync(transport, session, notify: false);
            await SendTransportAsync(connection, MediaTransportKind.State,
                new MediaTransportStateMessage(session.Id, state == RtcChannelState.Failed ? "failed" : "closed"));
        }
    }

    private async Task CloseSessionAsync(ConnectionMediaTransport transport, TransportSession session, bool notify)
    {
        lock (transport.Sync)
        {
            transport.Sessions.Remove(session.Id);
            if (transport.ActiveSession == session.Id) transport.ActiveSession = null;
        }
        if (session.Peer is { } peer)
        {
            if (transport.Connection.DetachDatagram(peer))
                _logger.LogInformation("Datagram media path closed for {ClientId}; media continues on the WebSocket", transport.Connection.ClientId);
            try { await peer.DisposeAsync(); } catch (Exception ex) { _logger.LogDebug(ex, "Closing a datagram peer failed"); }
        }
        if (notify)
            await SendTransportAsync(transport.Connection, MediaTransportKind.Close, new MediaTransportClose(session.Id, "superseded"));
    }

    private async Task CloseMediaTransportAsync(BoltHubConnection connection)
    {
        if (!_mediaTransports.TryRemove(connection.StreamId, out var transport))
            return;
        TransportSession[] sessions;
        lock (transport.Sync)
        {
            transport.Closed = true;
            sessions = transport.Sessions.Values.ToArray();
            transport.Sessions.Clear();
            transport.ActiveSession = null;
        }
        transport.Inbox.Writer.TryComplete();
        foreach (var session in sessions)
        {
            if (session.Peer is not { } peer) continue;
            connection.DetachDatagram(peer);
            try { await peer.DisposeAsync(); } catch { /* The connection is gone either way. */ }
        }
    }

    private async Task SendTransportAsync<T>(BoltHubConnection connection, MediaTransportKind kind, T message)
    {
        if (!connection.IsAlive) return;
        try { await connection.SendAsync(MediaTransportCodec.Encode(kind, message), _shutdownCts.Token); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogDebug(ex, "Media transport message to {ClientId} was not queued", connection.ClientId);
        }
    }

    // ── Inbound datagrams ──

    /// <summary>One message from a participant's data channel: processed as that connection's own frame, media only.</summary>
    private void OnDatagram(BoltHubConnection connection, ReadOnlyMemory<byte> data)
    {
        if (data.IsEmpty || !connection.IsRegistered || !connection.IsAlive || !_mediaEnabled)
            return;
        var type = (FrameType)data.Span[0];
        if (!DatagramFramePolicy.AcceptFromParticipant(type))
        {
            connection.RecordDatagramRejected();
            return;
        }
        if (type == FrameType.MediaBundle)
        {
            Span<Range> frames = stackalloc Range[MediaBundleCodec.MaxFrames];
            if (!MediaBundleCodec.TryRead(data.Span, frames, out var count))
            {
                connection.RecordDatagramRejected();
                return;
            }
            for (var index = 0; index < count; index++)
                DispatchDatagramFrame(connection, data.Span[frames[index]]);
            return;
        }
        DispatchDatagramFrame(connection, data.Span);
    }

    private void DispatchDatagramFrame(BoltHubConnection connection, ReadOnlySpan<byte> frame)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(frame.Length);
        frame.CopyTo(buffer);
        Task task;
        try
        {
            task = (FrameType)buffer[0] switch
            {
                FrameType.MediaFrame or FrameType.FecFrame => RouteMediaFrameAsync(connection, buffer, frame.Length, _shutdownCts.Token),
                FrameType.MediaFeedback or FrameType.MediaKeyRequest => RouteMediaFeedbackAsync(connection, buffer, frame.Length, _shutdownCts.Token),
                _ => Task.CompletedTask,
            };
        }
        catch (Exception ex)
        {
            ArrayPool<byte>.Shared.Return(buffer);
            _logger.LogDebug(ex, "A datagram frame from {ClientId} failed", connection.ClientId);
            return;
        }
        if (task.IsCompleted)
        {
            ArrayPool<byte>.Shared.Return(buffer);
            if (task.IsFaulted) _logger.LogDebug(task.Exception, "A datagram frame from {ClientId} failed", connection.ClientId);
            return;
        }
        _ = ReturnWhenDoneAsync(task, buffer);
    }

    private async Task ReturnWhenDoneAsync(Task task, byte[] buffer)
    {
        try { await task; }
        catch (Exception ex) { _logger.LogDebug(ex, "A datagram frame failed"); }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    // ── Loss reports and redundancy ──

    /// <summary>
    /// What a receiver reports about audio reaching it decides whether its audio goes out with redundancy:
    /// on above <see cref="BoltMediaTransportOptions.RedundancyOnLossPermille"/>, off only after a calm hold.
    /// </summary>
    private void ApplyReceiverLoss(ConnectionMediaTransport transport, int lossPermille)
    {
        var options = _mediaTransport!;
        var connection = transport.Connection;
        var now = Environment.TickCount64;
        if (lossPermille >= options.RedundancyOnLossPermille)
        {
            transport.LossQuietSince = 0;
            if (!connection.AudioRedundancy)
            {
                connection.AudioRedundancy = true;
                _logger.LogDebug("Audio redundancy on for {ClientId} at {Loss}‰ loss", connection.ClientId, lossPermille);
            }
            return;
        }
        if (!connection.AudioRedundancy) return;
        if (lossPermille >= options.RedundancyOffLossPermille) { transport.LossQuietSince = 0; return; }
        if (transport.LossQuietSince == 0) transport.LossQuietSince = now;
        else if (now - transport.LossQuietSince >= options.RedundancyHoldSeconds * 1000L)
        {
            connection.AudioRedundancy = false;
            transport.LossQuietSince = 0;
        }
    }

    /// <summary>
    /// Once a second: tell each participant on a datagram path how much of its audio arrived (it adds
    /// redundancy on its side the same way), and renew sessions whose credentials are about to expire.
    /// </summary>
    private async Task MediaTransportTickAsync()
    {
        if (_mediaTransport is not { } options || _shutdownCts.IsCancellationRequested) return;
        foreach (var transport in _mediaTransports.Values)
        {
            TransportSession? active;
            lock (transport.Sync)
                active = transport.ActiveSession is { } id ? transport.Sessions.GetValueOrDefault(id) : null;
            if (active?.Peer is not { State: RtcChannelState.Open } || transport.Closed) continue;

            long expected = 0, received = 0;
            foreach (var route in _activeMediaStreams.Values)
            {
                if (route.MediaType != MediaType.Audio || !ReferenceEquals(route.Sender, transport.Connection)) continue;
                var (e, r) = route.AudioSequences.Sample();
                expected += e;
                received += r;
            }
            if (expected > 0)
                await SendTransportAsync(transport.Connection, MediaTransportKind.Report,
                    new MediaTransportReport(active.Id, LossMath.Permille(expected, received)));

            if (!active.Renewing && active.Grant.ExpiresAt - DateTimeOffset.UtcNow <= options.RenewBefore)
            {
                active.Renewing = true;
                try { await StartSessionAsync(transport, _shutdownCts.Token); }
                catch (Exception ex) when (ex is not OperationCanceledException) { _logger.LogDebug(ex, "Datagram session renewal failed"); }
            }
        }
    }

    /// <summary>Tests only: let peers offer loopback candidates.</summary>
    internal bool AllowLoopback { get; set; }
}
