using Bolt.Client;
using Bolt.Protocol;
using Bolt.Protocol.Buffers;

namespace Bolt.Media;

/// <summary>
/// Receiver-side NACK tracker. Detects sequence number gaps and sends NackRequest
/// frames to the sender asking for retransmission of missing frames.
///
/// Waits a short grace period before sending NACK to allow out-of-order delivery
/// and FEC recovery to fill gaps first.
/// </summary>
public sealed class NackTracker : IAsyncDisposable
{
    private readonly BoltConnection _connection;
    private readonly Guid _streamId;
    private readonly HashSet<uint> _missingSeqs = [];
    private readonly HashSet<uint> _nackedSeqs = [];   // Already requested, avoid duplicates
    private readonly object _lock = new();
    private uint _highestReceived;
    private bool _initialized;

    private readonly CancellationTokenSource _cts = new();
    private Task? _nackTask;

    /// <summary>Max sequence numbers per single NackRequest frame.</summary>
    private const int MaxNacksPerRequest = 64;

    /// <summary>How often to check for gaps and send NACKs.</summary>
    private const int NackIntervalMs = 50;

    /// <summary>Don't NACK sequences older than this many frames behind highest received.</summary>
    private const int MaxNackAge = 512;

    public NackTracker(BoltConnection connection, Guid streamId)
    {
        _connection = connection;
        _streamId = streamId;
    }

    /// <summary>Start the periodic NACK sending loop.</summary>
    public void Start()
    {
        if (_nackTask != null) return;
        _nackTask = Task.Run(() => NackLoopAsync(_cts.Token));
    }

    /// <summary>
    /// Record a received frame sequence number.
    /// Detects gaps and queues missing sequences for NACK.
    /// </summary>
    public void RecordReceived(uint seq)
    {
        lock (_lock)
        {
            _missingSeqs.Remove(seq);
            _nackedSeqs.Remove(seq);

            if (!_initialized)
            {
                _highestReceived = seq;
                _initialized = true;
                return;
            }

            if (MediaSequence.IsNewer(seq, _highestReceived))
            {
                var distance = MediaSequence.ForwardDistance(_highestReceived, seq);
                if (distance <= MaxNackAge + 1)
                {
                    for (uint offset = 1; offset < distance; offset++)
                        _missingSeqs.Add(unchecked(_highestReceived + offset));
                }

                _highestReceived = seq;
            }

            _missingSeqs.RemoveWhere(s => MediaSequence.IsOlderThan(s, _highestReceived, MaxNackAge));
            _nackedSeqs.RemoveWhere(s => MediaSequence.IsOlderThan(s, _highestReceived, MaxNackAge));
        }
    }

    /// <summary>
    /// Called when a retransmitted frame is received — remove from tracking.
    /// </summary>
    public void RecordRetransmitReceived(uint seq)
    {
        lock (_lock)
        {
            _missingSeqs.Remove(seq);
            _nackedSeqs.Remove(seq);
        }
    }

    private async Task NackLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(NackIntervalMs));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(ct)) break;
            }
            catch (OperationCanceledException) { break; }

            uint[] toNack;
            lock (_lock)
            {
                // Find sequences that haven't been NACKed yet
                var pending = new List<uint>();
                foreach (var seq in _missingSeqs)
                {
                    if (!_nackedSeqs.Contains(seq))
                        pending.Add(seq);
                }

                if (pending.Count == 0) continue;

                pending.Sort((left, right) =>
                    MediaSequence.ForwardDistance(right, _highestReceived)
                        .CompareTo(MediaSequence.ForwardDistance(left, _highestReceived)));
                toNack = pending.Count > MaxNacksPerRequest
                    ? pending.GetRange(0, MaxNacksPerRequest).ToArray()
                    : pending.ToArray();

                foreach (var seq in toNack)
                    _nackedSeqs.Add(seq);
            }

            try
            {
                var writer = RentedBufferWriter.GetThreadLocal();
                BoltCodec.WriteNackRequest(writer, _streamId, toNack);
                await _connection.SendAsync(writer.WrittenMemory, ct);
            }
            catch (OperationCanceledException) { break; }
            catch { /* Connection closing */ }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        if (_nackTask != null)
            try { await _nackTask; } catch (OperationCanceledException) { }
        _cts.Dispose();
    }
}
