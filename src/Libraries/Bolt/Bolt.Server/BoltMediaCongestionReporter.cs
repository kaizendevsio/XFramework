using System.Buffers;
using Bolt.Protocol;

namespace Bolt.Server;

/// <summary>
/// Builds the relay-to-sender congestion report (<see cref="FrameType.MediaCongestion"/>) for one stream, the
/// REMB-style feedback that lets a sender follow its weakest receiver instead of guessing.
///
/// For each receiver the relay knows its queue's delay, the rate the link drained it at while it was backlogged,
/// and how much of what it was offered belonged to this stream. The report takes the worst receiver: the longest
/// queue, the smallest capacity share (<see cref="MediaCongestionData.AllowedKbps"/>), the lowest layer limit,
/// and every picture any receiver lost.
/// </summary>
internal sealed class MediaCongestionReporter
{
    /// <summary>Report cadence for a video stream; audio streams report half as often.</summary>
    public const int VideoIntervalMs = 250;
    public const int AudioIntervalMs = 500;
    /// <summary>A receiver whose queue is at least this old is backlogged even without drops.</summary>
    public const int BackloggedDelayMs = 40;

    private readonly object _sync = new();
    private readonly Dictionary<string, (long Total, long Stream, long Dropped, long Base, long Audio)> _previous = new(StringComparer.Ordinal);
    private long _lastReportAt = long.MinValue / 2;
    private uint _lastPicture;
    private bool _hasPicture;

    public MediaQueuingDelayEstimator Uplink { get; } = new();

    /// <summary>Record a frame's arrival from the sender. Video counts once per picture (its first fragment).</summary>
    public void ObserveArrival(uint timestamp, bool video, long nowMs)
    {
        lock (_sync)
        {
            if (video)
            {
                if (_hasPicture && timestamp == _lastPicture) return;
                _hasPicture = true;
                _lastPicture = timestamp;
            }
            Uplink.Observe(timestamp, video ? 90 : 48, nowMs);
        }
    }

    /// <summary>Cheap check before gathering recipients: is a report due?</summary>
    public bool IsDue(bool video, long nowMs) =>
        nowMs - Volatile.Read(ref _lastReportAt) >= (video ? VideoIntervalMs : AudioIntervalMs);

    /// <summary>Returns a report frame when one is due, otherwise null.</summary>
    public byte[]? TryBuild(Guid streamId, bool video, IReadOnlyList<BoltHubConnection> recipients, long nowMs)
    {
        lock (_sync)
        {
            if (nowMs - _lastReportAt < (video ? VideoIntervalMs : AudioIntervalMs))
                return null;
            _lastReportAt = nowMs;

            var report = new MediaCongestionData { StreamId = streamId, LayerLimit = 3 };
            var allowed = long.MaxValue;
            var worstDelay = 0;
            var dropped = 0L;
            var receivers = 0;
            var alive = new HashSet<string>(StringComparer.Ordinal);
            foreach (var recipient in recipients)
            {
                if (recipient.MediaQueue is not { } queue)
                    continue;
                receivers++;
                alive.Add(recipient.StreamId);
                var now = queue.Snapshot(streamId);
                var current = (now.TotalOfferedBytes, now.StreamOfferedBytes, now.StreamDroppedPictures, now.StreamBaseLosses, now.DroppedAudioFrames);
                if (!_previous.TryGetValue(recipient.StreamId, out var before))
                    before = current;
                _previous[recipient.StreamId] = current;

                var droppedHere = now.StreamDroppedPictures - before.Dropped;
                dropped += Math.Max(0, droppedHere);
                if (now.DroppedAudioFrames > before.Audio)
                    report.Flags |= MediaCongestionFlags.AudioDropping;
                if (now.StreamBaseLosses > before.Base)
                    report.Flags |= MediaCongestionFlags.BaseLayerLost;
                worstDelay = Math.Max(worstDelay, now.QueueDelayMs);
                report.LayerLimit = (byte)Math.Min(report.LayerLimit, now.LayerLimit);

                var backlogged = now.QueueDelayMs >= BackloggedDelayMs || droppedHere > 0;
                if (!backlogged || now.DeliveryKbps <= 0)
                    continue;
                // This stream's share of what the receiver was offered is its fair share of what it drained.
                var offered = now.TotalOfferedBytes - before.Total;
                var mine = now.StreamOfferedBytes - before.Stream;
                var share = offered > 0 ? Math.Clamp((double)mine / offered, 0, 1) : 1;
                allowed = Math.Min(allowed, (long)Math.Round(now.DeliveryKbps * share));
                report.Flags |= MediaCongestionFlags.Limited;
            }

            if (receivers == 0)
                return null;
            foreach (var gone in _previous.Keys.Where(key => !alive.Contains(key)).ToArray())
                _previous.Remove(gone);

            if (dropped > 0)
                report.Flags |= MediaCongestionFlags.Dropping;
            report.Receivers = (byte)Math.Min(receivers, byte.MaxValue);
            report.QueueDelayMs = (ushort)Math.Clamp(worstDelay, 0, ushort.MaxValue);
            report.UplinkDelayMs = (ushort)Uplink.DelayMs;
            report.AllowedKbps = allowed == long.MaxValue ? 0 : (uint)Math.Clamp(allowed, 1, uint.MaxValue);
            report.DroppedPictures = (ushort)Math.Clamp(dropped, 0, ushort.MaxValue);

            var writer = new ArrayBufferWriter<byte>(BoltCodec.MediaCongestionSize);
            BoltCodec.WriteMediaCongestion(writer, report);
            return writer.WrittenSpan.ToArray();
        }
    }
}
