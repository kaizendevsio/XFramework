using Bolt.Client;
using Bolt.Protocol;
using Bolt.Protocol.Buffers;

namespace Bolt.Media;

/// <summary>
/// Bandwidth prober that sends periodic probe packets to discover available
/// bandwidth faster than waiting for natural media frame feedback.
///
/// Probing strategy:
/// 1. Send a burst of small probe frames at increasing rates
/// 2. Measure delivery acknowledgements via MediaFeedback
/// 3. The highest rate that doesn't cause loss/delay increase = available bandwidth
///
/// Probe frames use the MediaFrame format with a special flag (DropEligible + probe marker)
/// so the receiver can distinguish them from real media and the hub can drop them under load.
///
/// Probing runs:
/// - On call start (initial bandwidth discovery, 2 second burst)
/// - After significant quality drop (re-probe to find new baseline)
/// - Periodically every 10 seconds (gentle maintenance probing)
/// </summary>
public sealed class BandwidthProber : IAsyncDisposable
{
    private readonly BoltConnection _connection;
    private readonly Guid _streamId;
    private CancellationTokenSource? _cts;
    private Task? _probeTask;
    private int _currentBitrateKbps;

    /// <summary>Probe frame flag: DropEligible (0x40) + probe marker (0x20).</summary>
    private const byte ProbeFlags = 0x60;

    /// <summary>Probe frame payload size (small, just enough to measure throughput).</summary>
    private const int ProbePayloadSize = 200;

    /// <summary>Interval between maintenance probe bursts.</summary>
    private const int MaintenanceProbeIntervalMs = 10_000;

    /// <summary>Number of frames in a probe burst.</summary>
    private const int BurstFrameCount = 10;

    /// <summary>Inter-frame delay within a burst (ms).</summary>
    private const int BurstInterFrameDelayMs = 5;

    /// <summary>Fired when probing estimates available bandwidth.</summary>
    public event Action<int>? OnBandwidthEstimated;

    public BandwidthProber(BoltConnection connection, Guid streamId, int initialBitrateKbps)
    {
        _connection = connection;
        _streamId = streamId;
        _currentBitrateKbps = initialBitrateKbps;
    }

    /// <summary>
    /// Start periodic bandwidth probing.
    /// </summary>
    public void Start()
    {
        if (_probeTask != null) return;
        _cts = new CancellationTokenSource();
        _probeTask = Task.Run(() => ProbeLoopAsync(_cts.Token));
    }

    /// <summary>
    /// Trigger an immediate probe burst (e.g., after quality drop).
    /// </summary>
    public void TriggerProbe()
    {
        _ = SendProbeBurstAsync(_cts?.Token ?? CancellationToken.None);
    }

    private async Task ProbeLoopAsync(CancellationToken ct)
    {
        // Initial probe burst (discover bandwidth at call start)
        await SendProbeBurstAsync(ct);

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(MaintenanceProbeIntervalMs));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(ct)) break;
            }
            catch (OperationCanceledException) { break; }

            await SendProbeBurstAsync(ct);
        }
    }

    /// <summary>
    /// Send a burst of probe frames at the current target rate.
    /// Probe frames are marked DropEligible so they can be safely dropped.
    /// </summary>
    private async Task SendProbeBurstAsync(CancellationToken ct)
    {
        var probePayload = new byte[ProbePayloadSize];
        // Fill with recognizable pattern for RTT measurement
        var timestamp = (uint)Environment.TickCount64;

        for (int i = 0; i < BurstFrameCount && !ct.IsCancellationRequested; i++)
        {
            // Embed probe sequence and send timestamp in payload
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(probePayload, (uint)i);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(probePayload.AsSpan(4), timestamp);

            try
            {
                var writer = RentedBufferWriter.GetThreadLocal();
                BoltCodec.WriteMediaFrame(writer, _streamId, (uint)(0xFFFF0000 | i), timestamp, ProbeFlags, probePayload);
                await _connection.SendAsync(writer.WrittenMemory, ct);

                if (BurstInterFrameDelayMs > 0)
                    await Task.Delay(BurstInterFrameDelayMs, ct);
            }
            catch (OperationCanceledException) { break; }
            catch { /* Connection may be closing */ }
        }
    }

    /// <summary>
    /// Process feedback to estimate bandwidth from probe results.
    /// Call this when MediaFeedback is received for the probe stream.
    /// </summary>
    public void ProcessFeedback(uint highestSeq, uint cumulativeLost, uint jitterX100)
    {
        // Calculate effective throughput from probe burst
        var delivered = BurstFrameCount - (int)cumulativeLost;
        if (delivered <= 0) return;

        var deliveryRate = (double)delivered / BurstFrameCount;
        var jitterMs = jitterX100 / 100.0;

        // Estimate: if delivery rate is high and jitter is low, bandwidth is available
        int estimatedKbps;
        if (deliveryRate > 0.95 && jitterMs < 20)
        {
            // Probe delivered cleanly — can likely increase
            estimatedKbps = (int)(_currentBitrateKbps * 1.2);
        }
        else if (deliveryRate > 0.8)
        {
            // Some loss — at capacity
            estimatedKbps = _currentBitrateKbps;
        }
        else
        {
            // Significant loss — reduce
            estimatedKbps = (int)(_currentBitrateKbps * deliveryRate);
        }

        estimatedKbps = Math.Max(estimatedKbps, 50);
        _currentBitrateKbps = estimatedKbps;
        OnBandwidthEstimated?.Invoke(estimatedKbps);
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
            if (_probeTask != null)
                try { await _probeTask; } catch (OperationCanceledException) { }
            _cts.Dispose();
        }
    }
}
