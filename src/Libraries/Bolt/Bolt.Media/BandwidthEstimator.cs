namespace Bolt.Media;

public enum QualityLayer { L1_Normal, L2_ReduceResolution, L3_CompressLZ4, L4_CompressZstd, L5_AudioOnly }

public sealed class BandwidthEstimator
{
    private long _bytesSent;
    private long _windowStartTicks;
    private int _targetKbps;
    private int _actualKbps;
    private QualityLayer _currentLayer = QualityLayer.L1_Normal;

    public int TargetThroughputKbps { get => _targetKbps; set => _targetKbps = value; }
    public int ActualThroughputKbps => _actualKbps;
    public QualityLayer CurrentLayer => _currentLayer;
    public bool ShouldCompress => _currentLayer >= QualityLayer.L3_CompressLZ4;

    public BandwidthEstimator(int targetKbps = 5000)
    {
        _targetKbps = targetKbps;
        _windowStartTicks = Environment.TickCount64;
    }

    public void RecordBytesSent(int bytes)
    {
        Interlocked.Add(ref _bytesSent, bytes);
        var elapsed = Environment.TickCount64 - Interlocked.Read(ref _windowStartTicks);
        if (elapsed >= 500)
        {
            var sent = Interlocked.Exchange(ref _bytesSent, 0);
            Interlocked.Exchange(ref _windowStartTicks, Environment.TickCount64);
            _actualKbps = (int)(sent * 8 / Math.Max(elapsed, 1));
            UpdateLayer();
        }
    }

    private void UpdateLayer()
    {
        var ratio = _targetKbps > 0 ? (double)_actualKbps / _targetKbps : 1.0;
        _currentLayer = ratio switch
        {
            >= 0.70 => QualityLayer.L1_Normal,
            >= 0.50 => QualityLayer.L2_ReduceResolution,
            >= 0.40 => QualityLayer.L3_CompressLZ4,
            >= 0.25 => QualityLayer.L4_CompressZstd,
            _ => QualityLayer.L5_AudioOnly,
        };
    }
}
