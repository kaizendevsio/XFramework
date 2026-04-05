using Bolt.Client;
namespace Bolt.Media;

/// <summary>
/// Represents a simulcast encoding layer.
/// In simulcast, the sender encodes the same video at multiple resolutions/bitrates.
/// The SFU hub selects which layer to forward to each receiver based on their
/// available bandwidth (reported via MediaFeedback).
///
/// Layer selection:
/// - High: Full resolution, high bitrate (default for good connections)
/// - Mid:  Half resolution, medium bitrate (for moderate bandwidth)
/// - Low:  Quarter resolution, low bitrate (for constrained connections)
/// </summary>
public sealed class SimulcastLayer
{
    public SimulcastLayerId Id { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int BitrateKbps { get; init; }
    public int Framerate { get; init; }

    /// <summary>The stream ID for this layer (each layer is a separate media stream).</summary>
    public Guid StreamId { get; init; }

    /// <summary>Associated BoltMediaStream (set after creation).</summary>
    public BoltMediaStream? Stream { get; internal set; }
}

/// <summary>Simulcast layer identifier.</summary>
public enum SimulcastLayerId : byte
{
    /// <summary>Quarter resolution (e.g., 320x180 @ 150kbps).</summary>
    Low = 0,
    /// <summary>Half resolution (e.g., 640x360 @ 500kbps).</summary>
    Mid = 1,
    /// <summary>Full resolution (e.g., 1280x720 @ 2000kbps).</summary>
    High = 2,
}

/// <summary>
/// Manages simulcast layers for a video track.
/// Creates 3 encoding layers at different resolutions.
/// </summary>
public sealed class SimulcastManager
{
    private readonly SimulcastLayer[] _layers;

    public IReadOnlyList<SimulcastLayer> Layers => _layers;

    /// <summary>
    /// Create simulcast layers based on the source resolution.
    /// Standard 3-layer simulcast: full, half, quarter.
    /// </summary>
    public SimulcastManager(int sourceWidth, int sourceHeight, int sourceBitrateKbps, int sourceFramerate)
    {
        _layers =
        [
            new SimulcastLayer
            {
                Id = SimulcastLayerId.Low,
                Width = sourceWidth / 4,
                Height = sourceHeight / 4,
                BitrateKbps = Math.Max(100, sourceBitrateKbps / 10),
                Framerate = Math.Min(15, sourceFramerate),
                StreamId = Guid.NewGuid(),
            },
            new SimulcastLayer
            {
                Id = SimulcastLayerId.Mid,
                Width = sourceWidth / 2,
                Height = sourceHeight / 2,
                BitrateKbps = sourceBitrateKbps / 4,
                Framerate = sourceFramerate,
                StreamId = Guid.NewGuid(),
            },
            new SimulcastLayer
            {
                Id = SimulcastLayerId.High,
                Width = sourceWidth,
                Height = sourceHeight,
                BitrateKbps = sourceBitrateKbps,
                Framerate = sourceFramerate,
                StreamId = Guid.NewGuid(),
            },
        ];
    }

    /// <summary>
    /// Select the best layer for a receiver based on their available bandwidth.
    /// </summary>
    public SimulcastLayer SelectLayer(int availableBandwidthKbps)
    {
        // Pick the highest layer that fits within the available bandwidth
        for (int i = _layers.Length - 1; i >= 0; i--)
        {
            if (_layers[i].BitrateKbps <= availableBandwidthKbps)
                return _layers[i];
        }
        return _layers[0]; // Fallback to lowest
    }

    /// <summary>
    /// Get a specific layer by ID.
    /// </summary>
    public SimulcastLayer GetLayer(SimulcastLayerId id) => _layers[(int)id];
}
