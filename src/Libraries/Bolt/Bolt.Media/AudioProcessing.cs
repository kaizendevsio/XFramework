using System.Buffers.Binary;

namespace Bolt.Media;

/// <summary>
/// Packet Loss Concealment for audio frames.
///
/// When a frame is lost (gap in sequence numbers), PLC generates a substitute:
/// 1. First lost frame: replay last frame at reduced volume (fade-out)
/// 2. Subsequent losses: generate comfort noise (low-level noise)
/// 3. After 3+ consecutive losses: output silence
///
/// This avoids the jarring clicks/pops that occur when decoded audio has gaps.
/// Works with raw PCM samples (after Opus decode) or with encoded frames
/// by providing the last good frame as a replacement.
/// </summary>
public sealed class PacketLossConcealment
{
    private byte[]? _lastGoodFrame;
    private int _consecutiveLosses;
    private readonly int _frameSize;    // Expected frame size in bytes
    private readonly Random _rng = new();

    /// <summary>Max consecutive lost frames before outputting silence.</summary>
    private const int MaxFadeFrames = 3;

    /// <summary>Comfort noise level (0.0 = silence, 1.0 = full volume). Very low.</summary>
    private const float ComfortNoiseLevel = 0.02f;

    public PacketLossConcealment(int frameSize = 960)
    {
        _frameSize = frameSize;
    }

    /// <summary>
    /// Record a successfully received frame for future PLC reference.
    /// </summary>
    public void RecordGoodFrame(ReadOnlySpan<byte> frame)
    {
        _consecutiveLosses = 0;
        _lastGoodFrame = frame.ToArray();
    }

    /// <summary>
    /// Generate a concealment frame to replace a lost frame.
    /// Returns a frame that can be played to mask the gap.
    /// </summary>
    public byte[] GenerateConcealmentFrame()
    {
        _consecutiveLosses++;

        if (_consecutiveLosses == 1 && _lastGoodFrame != null)
        {
            // First loss: fade-out of last good frame (75% volume)
            return ApplyGain(_lastGoodFrame, 0.75f);
        }

        if (_consecutiveLosses == 2 && _lastGoodFrame != null)
        {
            // Second loss: further fade (40% volume)
            return ApplyGain(_lastGoodFrame, 0.40f);
        }

        if (_consecutiveLosses <= MaxFadeFrames)
        {
            // Generate comfort noise
            return GenerateComfortNoise();
        }

        // Beyond max fade: silence
        return new byte[_lastGoodFrame?.Length ?? _frameSize];
    }

    /// <summary>
    /// Apply a gain multiplier to PCM-16LE audio samples.
    /// </summary>
    private static byte[] ApplyGain(byte[] frame, float gain)
    {
        var output = new byte[frame.Length];
        for (int i = 0; i + 1 < frame.Length; i += 2)
        {
            var sample = BinaryPrimitives.ReadInt16LittleEndian(frame.AsSpan(i));
            var adjusted = (short)(sample * gain);
            BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(i), adjusted);
        }
        return output;
    }

    /// <summary>
    /// Generate low-level comfort noise (dithered near zero).
    /// </summary>
    private byte[] GenerateComfortNoise()
    {
        var size = _lastGoodFrame?.Length ?? _frameSize;
        var noise = new byte[size];
        for (int i = 0; i + 1 < noise.Length; i += 2)
        {
            var sample = (short)((_rng.NextDouble() * 2 - 1) * short.MaxValue * ComfortNoiseLevel);
            BinaryPrimitives.WriteInt16LittleEndian(noise.AsSpan(i), sample);
        }
        return noise;
    }
}

/// <summary>
/// Voice Activity Detection (VAD) for Discontinuous Transmission (DTX).
///
/// Energy-based VAD: measures the energy of audio frames and classifies them
/// as voice or silence. During silence, frames are suppressed (not sent),
/// saving ~60-70% bandwidth during typical conversation pauses.
///
/// Uses a dual-threshold approach to avoid rapid toggling:
/// - Speech onset: energy must exceed upper threshold for N consecutive frames
/// - Speech offset: energy must stay below lower threshold for M consecutive frames
///
/// When DTX is active and silence is detected, the sender can:
/// 1. Stop sending media frames entirely (most bandwidth savings)
/// 2. Send a single "silence indicator" frame periodically (every 500ms)
/// </summary>
public sealed class VoiceActivityDetector
{
    private readonly float _speechOnsetThreshold;
    private readonly float _speechOffsetThreshold;
    private readonly int _onsetHangover;    // Frames above threshold before declaring speech
    private readonly int _offsetHangover;   // Frames below threshold before declaring silence

    private int _aboveThresholdCount;
    private int _belowThresholdCount;
    private bool _isSpeech;
    private float _noiseFloor = 0.001f;  // Adaptive noise floor
    private const float NoiseFloorAdaptRate = 0.01f;

    /// <summary>True if the current frame is classified as speech.</summary>
    public bool IsSpeech => _isSpeech;

    /// <summary>Number of consecutive silence frames detected.</summary>
    public int SilenceFrameCount => _belowThresholdCount;

    /// <summary>
    /// Create a VAD with configurable thresholds.
    /// </summary>
    /// <param name="speechOnsetDb">dB above noise floor to trigger speech (default -26 dBFS).</param>
    /// <param name="speechOffsetDb">dB above noise floor for speech offset (default -35 dBFS).</param>
    /// <param name="onsetFrames">Consecutive frames above threshold before speech (default 3).</param>
    /// <param name="offsetFrames">Consecutive frames below threshold before silence (default 15, ~300ms at 20ms frames).</param>
    public VoiceActivityDetector(
        float speechOnsetDb = -26f,
        float speechOffsetDb = -35f,
        int onsetFrames = 3,
        int offsetFrames = 15)
    {
        _speechOnsetThreshold = MathF.Pow(10, speechOnsetDb / 20f);
        _speechOffsetThreshold = MathF.Pow(10, speechOffsetDb / 20f);
        _onsetHangover = onsetFrames;
        _offsetHangover = offsetFrames;
    }

    /// <summary>
    /// Analyze an audio frame (PCM-16LE) and classify as speech or silence.
    /// Returns true if the frame contains speech.
    /// </summary>
    public bool Analyze(ReadOnlySpan<byte> pcm16Frame)
    {
        var energy = ComputeRmsEnergy(pcm16Frame);

        // Adapt noise floor slowly during silence
        if (!_isSpeech)
        {
            _noiseFloor = _noiseFloor * (1 - NoiseFloorAdaptRate) + energy * NoiseFloorAdaptRate;
            _noiseFloor = Math.Max(_noiseFloor, 1e-6f); // Prevent zero
        }

        var normalizedEnergy = energy / _noiseFloor;

        if (normalizedEnergy > _speechOnsetThreshold)
        {
            _aboveThresholdCount++;
            _belowThresholdCount = 0;

            if (_aboveThresholdCount >= _onsetHangover)
                _isSpeech = true;
        }
        else if (normalizedEnergy < _speechOffsetThreshold)
        {
            _belowThresholdCount++;
            _aboveThresholdCount = 0;

            if (_belowThresholdCount >= _offsetHangover)
                _isSpeech = false;
        }

        return _isSpeech;
    }

    /// <summary>
    /// Compute RMS energy of PCM-16LE samples, normalized to [0, 1].
    /// </summary>
    private static float ComputeRmsEnergy(ReadOnlySpan<byte> pcm16)
    {
        if (pcm16.Length < 2) return 0;

        double sumSquares = 0;
        int sampleCount = 0;

        for (int i = 0; i + 1 < pcm16.Length; i += 2)
        {
            var sample = BinaryPrimitives.ReadInt16LittleEndian(pcm16.Slice(i));
            var normalized = sample / (double)short.MaxValue;
            sumSquares += normalized * normalized;
            sampleCount++;
        }

        return sampleCount > 0 ? (float)Math.Sqrt(sumSquares / sampleCount) : 0f;
    }
}

/// <summary>
/// Media frame flags for DTX (Discontinuous Transmission).
/// </summary>
public static class DtxFlags
{
    /// <summary>Frame flag bit indicating this is a silence/comfort noise indicator frame.</summary>
    public const byte SilenceIndicator = 0x04;

    /// <summary>Interval between silence indicator frames (in milliseconds).</summary>
    public const int SilenceIndicatorIntervalMs = 500;
}
