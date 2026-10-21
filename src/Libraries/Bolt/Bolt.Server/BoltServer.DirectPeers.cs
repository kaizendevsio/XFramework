using System.Collections.Concurrent;
using System.Security.Cryptography;
using Bolt.Protocol;

namespace Bolt.Server;

public sealed partial class BoltServer
{
    // Only authenticated, admitted participants can be paired. No client-supplied destination IDs.
    private readonly ConcurrentDictionary<string, DirectPair> _directPairs = new();
    private readonly SemaphoreSlim _directGate = new(1, 1);

    private sealed class DirectPair(string id, Guid callId, ConnectionMediaTransport offerer,
        ConnectionMediaTransport answerer, DateTimeOffset expires)
    {
        public string Id => id;
        public Guid CallId => callId;
        public ConnectionMediaTransport Offerer => offerer;
        public ConnectionMediaTransport Answerer => answerer;
        public DateTimeOffset Expires => expires;
        public int Messages;
        public bool Contains(ConnectionMediaTransport t) => ReferenceEquals(t, offerer) || ReferenceEquals(t, answerer);
        public ConnectionMediaTransport Other(ConnectionMediaTransport t) => ReferenceEquals(t, offerer) ? answerer : offerer;
    }

    private bool PairAuthorized(DirectPair pair) =>
        !pair.Offerer.Closed && !pair.Answerer.Closed && pair.Offerer.Connection.IsAlive && pair.Answerer.Connection.IsAlive &&
        _activeCalls.TryGetValue(pair.CallId, out var call) && IsCallMediaActive(call) &&
        GetParticipantSnapshot(call) is { Count: 2 } peers &&
        peers.Contains(pair.Offerer.Connection) && peers.Contains(pair.Answerer.Connection) &&
        !_activeCalls.Values.Any(other => other.CallId != pair.CallId && IsCallMediaActive(other) &&
            GetParticipantSnapshot(other).Any(p => ReferenceEquals(p, pair.Offerer.Connection) || ReferenceEquals(p, pair.Answerer.Connection)));

    private async Task CloseInvalidDirectPairsAsync()
    {
        foreach (var pair in _directPairs.Values)
            if (!PairAuthorized(pair)) await CloseDirectPairAsync(pair, "peer-ended");
    }

    private async Task CloseDirectPairAsync(DirectPair pair, string reason)
    {
        if (!_directPairs.TryRemove(pair.Id, out _)) return;
        await SendTransportAsync(pair.Offerer.Connection, MediaTransportKind.Close, new MediaTransportClose(pair.Id, reason));
        await SendTransportAsync(pair.Answerer.Connection, MediaTransportKind.Close, new MediaTransportClose(pair.Id, reason));
    }

    private async Task UpdateDirectPeersAsync()
    {
        if (!await _directGate.WaitAsync(0)) return;
        try
        {
            foreach (var pair in _directPairs.Values)
            {
                if (_activeCalls.TryGetValue(pair.CallId, out var admitted) && admitted.HostManagedGroup) ScheduleGroupAuthorizationRenewal(admitted);
                var renewing = DateTimeOffset.UtcNow >= pair.Expires.AddSeconds(-60);
                if (renewing) pair.Offerer.DirectAttempted = pair.Answerer.DirectAttempted = false;
                if (!PairAuthorized(pair) || renewing)
                    await CloseDirectPairAsync(pair, "peer-ended");
            }
            foreach (var call in _activeCalls.Values)
            {
                if (!IsCallMediaActive(call) || GetParticipantSnapshot(call) is not { Count: 2 } peers) continue;
                if (!_mediaTransports.TryGetValue(peers[0].StreamId, out var a) ||
                    !_mediaTransports.TryGetValue(peers[1].StreamId, out var b) ||
                    !a.DirectPeer || !b.DirectPeer || a.DirectAttempted || b.DirectAttempted || a.Closed || b.Closed) continue;
                TransportSession? sa, sb;
                lock (a.Sync) sa = a.Sessions.Values.FirstOrDefault();
                lock (b.Sync) sb = b.Sessions.Values.FirstOrDefault();
                if (sa is null || sb is null) continue;
                a.DirectAttempted = b.DirectAttempted = true;
                var id = "p-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
                var expires = sa.Grant.ExpiresAt < sb.Grant.ExpiresAt ? sa.Grant.ExpiresAt : sb.Grant.ExpiresAt;
                var pair = new DirectPair(id, call.CallId, a, b, expires);
                if (!PairAuthorized(pair)) continue;
                _directPairs[id] = pair;
                // Queue the answerer's configuration first, before the offerer can send its offer.
                await Configure(b, sb, true);
                await Configure(a, sa, false);
                async Task Configure(ConnectionMediaTransport t, TransportSession s, bool answerer) =>
                    await SendTransportAsync(t.Connection, MediaTransportKind.Config, new MediaTransportConfig(
                        id, s.Grant.Client.ToArray(), "all", _mediaTransport!.MaxMessageBytes, expires.ToUnixTimeSeconds(),
                        Features: [MediaTransportFeatures.Nack, MediaTransportFeatures.TransportFeedback], DirectPeer: true, Answerer: answerer));
            }
        }
        finally { _directGate.Release(); }
    }

    private async Task<bool> ForwardPeerSignalAsync(ConnectionMediaTransport sender, MediaTransportKind kind, byte[] payload)
    {
        var id = kind switch
        {
            MediaTransportKind.Offer or MediaTransportKind.Answer => MediaTransportCodec.Decode<MediaTransportDescription>(payload)?.Session,
            MediaTransportKind.Candidate => MediaTransportCodec.Decode<MediaTransportCandidate>(payload)?.Session,
            MediaTransportKind.State => MediaTransportCodec.Decode<MediaTransportStateMessage>(payload)?.Session,
            MediaTransportKind.Report => MediaTransportCodec.Decode<MediaTransportReport>(payload)?.Session,
            MediaTransportKind.Close => MediaTransportCodec.Decode<MediaTransportClose>(payload)?.Session,
            _ => null,
        };
        if (id is null || !id.StartsWith("p-", StringComparison.Ordinal)) return false;
        if (!_directPairs.TryGetValue(id, out var pair) || !pair.Contains(sender)) return true;
        if (!PairAuthorized(pair) || kind == MediaTransportKind.Close || Interlocked.Increment(ref pair.Messages) > 20_000)
        {
            await CloseDirectPairAsync(pair, "peer-ended");
            return true;
        }
        if (kind == MediaTransportKind.Offer && !ReferenceEquals(sender, pair.Offerer) ||
            kind == MediaTransportKind.Answer && !ReferenceEquals(sender, pair.Answerer)) return true;
        // Same authenticated call, exact connection, bounded payload. Never log SDP/addresses/credentials.
        var writer = new System.Buffers.ArrayBufferWriter<byte>();
        MediaTransportCodec.Write(writer, kind, payload);
        await pair.Other(sender).Connection.SendAsync(writer.WrittenMemory, _shutdownCts.Token);
        return true;
    }
}
