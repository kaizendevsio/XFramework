using System.Buffers.Binary;
using System.Threading.Channels;
using Bolt.Client;
using Bolt.Protocol;
using Bolt.Protocol.Buffers;

namespace Bolt.Media;

/// <summary>
/// Decoded media frame data delivered to consumers.
/// </summary>
public readonly record struct MediaFrameData(uint SequenceNumber, uint Timestamp, ReadOnlyMemory<byte> Data, bool IsKeyframe);

/// <summary>
/// A media-specific stream for sending/receiving encoded audio/video frames.
/// Unlike <see cref="BoltStream"/> (general-purpose byte streaming), this is optimized
/// for real-time media: sequence numbers, timestamps, keyframe flags, and drop-oldest
/// back-pressure to keep latency bounded.
///
/// Fully integrated features:
/// - E2E encryption (AES-256-GCM via MediaEncryption)
/// - FEC (XOR parity for frame recovery)
/// - NACK retransmission (sender-side buffer + receiver-side gap detection)
/// - Adaptive bitrate (loss-based + delay-based congestion control)
/// - VAD/DTX (voice activity detection, silence suppression for audio)
/// - PLC (packet loss concealment for audio)
/// - Jitter buffer (adaptive delay for ordered playback)
/// - QUIC datagram transport (unreliable path for drop-eligible frames)
/// - P2P direct connection (seamless hub↔direct switching)
/// - Bandwidth probing (periodic probes for bandwidth discovery)
/// </summary>
public sealed class BoltMediaStream : IAsyncDisposable
{
    private BoltConnection _connection;
    private readonly Channel<MediaFrameData> _inbound;
    private uint _nextSequence;
    private uint _timestampCounter;
    private readonly uint _timestampIncrement;
    private bool _closed;
    private FecEncoder? _fecEncoder;
    private FecDecoder? _fecDecoder;
    private uint _lastReceivedSeq;

    // Encryption
    private IMediaEncryption? _encryption;

    // NACK retransmission
    private RetransmitBuffer? _retransmitBuffer;
    private NackTracker? _nackTracker;

    // QUIC datagram transport
    private Func<ReadOnlyMemory<byte>, ValueTask>? _datagramSend;

    // Congestion control
    private DelayBasedController? _delayController;
    private BandwidthProber? _prober;

    // Audio processing (VAD/DTX + PLC)
    private VoiceActivityDetector? _vad;
    private PacketLossConcealment? _plc;
    private int _silenceFrameCount;

    // Jitter buffer (opt-in)
    private MediaJitterBuffer? _jitterBuffer;

    // P2P direct connection
    private DirectConnectionManager? _directManager;

    /// <summary>Unique identifier for this media stream.</summary>
    public Guid StreamId { get; }

    /// <summary>The call this media stream belongs to.</summary>
    public Guid CallId { get; }

    /// <summary>True if this is an audio stream; false for video.</summary>
    public bool IsAudio { get; }

    /// <summary>True if E2E encryption is active.</summary>
    public bool IsEncrypted => _encryption?.IsReady == true;

    /// <summary>True if QUIC datagram transport is available.</summary>
    public bool HasDatagramTransport => _datagramSend != null;

    /// <summary>True if using P2P direct connection instead of hub.</summary>
    public bool IsDirectConnection => _directManager?.IsDirectActive == true;

    /// <summary>Fired when congestion control recommends a bitrate change (kbps).</summary>
    public event Action<int>? OnBitrateChanged;

    /// <summary>Fired when a keyframe is needed (congestion or new participant).</summary>
    public event Action? OnKeyframeNeeded;

    /// <summary>Raise OnBitrateChanged from external controllers.</summary>
    internal void RaiseBitrateChanged(int kbps) => OnBitrateChanged?.Invoke(kbps);

    /// <summary>Raise OnKeyframeNeeded from external controllers.</summary>
    internal void RaiseKeyframeNeeded() => OnKeyframeNeeded?.Invoke();

    public BoltMediaStream(BoltConnection connection, Guid streamId, Guid callId, bool isAudio)
    {
        _connection = connection;
        StreamId = streamId;
        CallId = callId;
        IsAudio = isAudio;
        _timestampIncrement = isAudio ? 960u : 3000u;
        _inbound = Channel.CreateBounded<MediaFrameData>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    // ── Feature enablement ───────────────────────────────────────

    /// <summary>Enable FEC (XOR parity across frame groups).</summary>
    public void EnableFec(int groupSize = 4)
    {
        _fecEncoder = new FecEncoder(groupSize);
        _fecDecoder = new FecDecoder();
    }

    /// <summary>Enable E2E encryption using .NET-native crypto. Returns the instance for key exchange.</summary>
    /// <remarks>Not available in Blazor WASM — use <see cref="SetEncryption(IMediaEncryption)"/> with <see cref="ExternalMediaEncryption"/> instead.</remarks>
    public MediaEncryption EnableEncryption()
    {
        var enc = new MediaEncryption();
        _encryption = enc;
        return enc;
    }

    /// <summary>Set encryption from external provider (supports Blazor WASM via ExternalMediaEncryption).</summary>
    public void SetEncryption(IMediaEncryption encryption) => _encryption = encryption;

    /// <summary>Set QUIC datagram transport for unreliable sends.</summary>
    public void SetDatagramTransport(Func<ReadOnlyMemory<byte>, ValueTask> datagramSend) => _datagramSend = datagramSend;

    /// <summary>Enable NACK retransmission (sender buffer + receiver gap detection).</summary>
    public void EnableNack(int retransmitBufferSize = 256)
    {
        _retransmitBuffer = new RetransmitBuffer(retransmitBufferSize);
        _nackTracker = new NackTracker(_connection, StreamId);
        _nackTracker.Start();
    }

    /// <summary>
    /// Enable delay-based congestion control (GCC-style).
    /// Works alongside loss-based ABR in AdaptiveBitrateController.
    /// Fires OnBitrateChanged when adjustment is needed.
    /// </summary>
    public void EnableDelayBasedControl(int initialBitrateKbps)
    {
        _delayController = new DelayBasedController(initialBitrateKbps, IsAudio);
        _delayController.OnBitrateChanged += kbps => OnBitrateChanged?.Invoke(kbps);
    }

    /// <summary>
    /// Enable bandwidth probing (periodic probe bursts for bandwidth discovery).
    /// </summary>
    public void EnableBandwidthProbing(int initialBitrateKbps)
    {
        _prober = new BandwidthProber(_connection, StreamId, initialBitrateKbps);
        _prober.OnBandwidthEstimated += kbps => OnBitrateChanged?.Invoke(kbps);
        _prober.Start();
    }

    /// <summary>
    /// Enable VAD/DTX for audio streams. Suppresses sending during silence.
    /// </summary>
    public void EnableVad()
    {
        if (!IsAudio) return;
        _vad = new VoiceActivityDetector();
    }

    /// <summary>
    /// Enable packet loss concealment for audio streams.
    /// Generates fade-out / comfort noise for lost frames.
    /// </summary>
    public void EnablePlc(int frameSize = 960)
    {
        if (!IsAudio) return;
        _plc = new PacketLossConcealment(frameSize);
    }

    /// <summary>
    /// Enable jitter buffer for ordered, delay-smoothed playback.
    /// </summary>
    public MediaJitterBuffer EnableJitterBuffer()
    {
        _jitterBuffer = new MediaJitterBuffer(IsAudio);
        _jitterBuffer.Start();
        return _jitterBuffer;
    }

    /// <summary>
    /// Set the P2P direct connection manager. When direct connection activates,
    /// media frames are sent directly to the peer instead of through the hub.
    /// </summary>
    public void SetDirectConnectionManager(DirectConnectionManager manager)
    {
        _directManager = manager;
        manager.OnConnectionModeChanged += direct =>
        {
            // Seamlessly switch the send connection
            if (direct && manager.ActiveConnection != _connection)
                _connection = manager.ActiveConnection;
            else if (!direct)
                _connection = manager.ActiveConnection; // Falls back to hub
        };
    }

    // ── FEC inbound ──────────────────────────────────────────────

    public void EnqueueFecFrame(uint groupStart, byte groupSize, ReadOnlyMemory<byte> payload)
    {
        if (_fecDecoder == null) return;
        var span = payload.Span;
        var lengths = new int[groupSize];
        for (int i = 0; i < groupSize; i++)
            lengths[i] = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(i * 4));
        var parityData = payload.Slice(groupSize * 4);
        _fecDecoder.AddFecFrame(groupStart, groupSize, parityData, lengths);
    }

    // ── Send path ────────────────────────────────────────────────

    /// <summary>
    /// Send an encoded media frame to the remote peer.
    /// Applies: VAD → encryption → retransmit buffer → QUIC/reliable send → FEC.
    /// </summary>
    public async ValueTask SendFrameAsync(ReadOnlyMemory<byte> encodedData, bool isKeyframe = false, CancellationToken ct = default)
    {
        if (_closed) return;

        // VAD/DTX: skip silence frames for audio (except periodic silence indicators)
        if (_vad != null && !isKeyframe)
        {
            var isSpeech = _vad.Analyze(encodedData.Span);
            if (!isSpeech)
            {
                _silenceFrameCount++;
                // Send periodic silence indicator every ~500ms (25 frames at 20ms)
                if (_silenceFrameCount % 25 != 0)
                    return; // Suppress this frame
                // else: send silence indicator
            }
            else
            {
                _silenceFrameCount = 0;
            }
        }

        var seq = _nextSequence++;
        var ts = _timestampCounter;
        _timestampCounter += _timestampIncrement;

        byte flags = 0;
        if (isKeyframe) flags |= 0x01;
        if (_vad != null && !_vad.IsSpeech) flags |= DtxFlags.SilenceIndicator;

        // Encrypt payload if enabled
        ReadOnlyMemory<byte> payload;
        if (_encryption?.IsReady == true)
        {
            flags |= 0x10; // encrypted flag
            payload = _encryption.Encrypt(encodedData.Span, seq, StreamId);
        }
        else
        {
            payload = encodedData;
        }

        // Store in retransmit buffer before sending
        _retransmitBuffer?.Store(seq, ts, flags, payload);

        // Determine transport: QUIC datagram vs reliable
        // FIX: check drop-eligible BEFORE writing the frame (flag must be in header)
        var useDatagramTransport = _datagramSend != null
            && !isKeyframe
            && payload.Length <= QuicDatagramHelper.MaxDatagramSize
            && (flags & 0x40) != 0; // Only if already marked drop-eligible

        // For non-keyframe video delta frames, auto-mark as drop-eligible for datagram
        if (_datagramSend != null && !isKeyframe && !IsAudio && payload.Length <= QuicDatagramHelper.MaxDatagramSize)
        {
            flags |= 0x40; // drop-eligible
            useDatagramTransport = true;
        }

        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteMediaFrame(writer, StreamId, seq, ts, flags, payload.Span);

        if (useDatagramTransport)
        {
            await _datagramSend!(writer.WrittenMemory);
        }
        else
        {
            // Use direct connection if available, otherwise hub
            var conn = _directManager?.IsDirectActive == true ? _directManager.ActiveConnection : _connection;
            await conn.SendAsync(writer.WrittenMemory, ct);
        }
        writer.Reset();

        // FEC operates on the (potentially encrypted) payload
        if (_fecEncoder != null)
        {
            var fecResult = _fecEncoder.AddFrame(seq, payload);
            if (fecResult != null)
            {
                var lengthBytes = new byte[fecResult.OriginalLengths.Length * 4];
                for (int i = 0; i < fecResult.OriginalLengths.Length; i++)
                    BinaryPrimitives.WriteInt32LittleEndian(lengthBytes.AsSpan(i * 4), fecResult.OriginalLengths[i]);
                var fecPayload = new byte[lengthBytes.Length + fecResult.ParityData.Length];
                lengthBytes.CopyTo(fecPayload, 0);
                fecResult.ParityData.CopyTo(fecPayload, lengthBytes.Length);

                var fecWriter = RentedBufferWriter.GetThreadLocal();
                BoltCodec.WriteFecFrame(fecWriter, StreamId, fecResult.GroupStartSequence, fecResult.GroupSize, fecPayload);
                var conn = _directManager?.IsDirectActive == true ? _directManager.ActiveConnection : _connection;
                await conn.SendAsync(fecWriter.WrittenMemory, ct);
                fecWriter.Reset();
            }
        }
    }

    // ── NACK handling ────────────────────────────────────────────

    public async ValueTask HandleNackAsync(uint[] missingSequences, CancellationToken ct = default)
    {
        if (_retransmitBuffer == null) return;

        var conn = _directManager?.IsDirectActive == true ? _directManager.ActiveConnection : _connection;
        foreach (var seq in missingSequences)
        {
            if (_retransmitBuffer.TryGet(seq, out var frame) && frame.Payload != null)
            {
                var writer = RentedBufferWriter.GetThreadLocal();
                BoltCodec.WriteMediaFrame(writer, StreamId, frame.SequenceNumber, frame.Timestamp, frame.Flags, frame.Payload);
                await conn.SendAsync(writer.WrittenMemory, ct);
                writer.Reset();
            }
        }
    }

    // ── Receive path ─────────────────────────────────────────────

    /// <summary>
    /// Called internally by the receive loop to deliver an inbound frame.
    /// Applies: decrypt → PLC tracking → delay controller → jitter buffer or direct delivery.
    /// </summary>
    public void EnqueueFrame(uint seq, uint timestamp, ReadOnlyMemory<byte> data, byte flags)
    {
        var isKeyframe = (flags & 0x01) != 0;
        var isEncrypted = (flags & 0x10) != 0;

        // Decrypt if needed
        byte[] frameData;
        if (isEncrypted && _encryption?.IsReady == true)
        {
            try
            {
                frameData = _encryption.Decrypt(data.Span, seq, StreamId);
            }
            catch
            {
                return; // Corrupted/tampered frame
            }
        }
        else
        {
            frameData = new byte[data.Length];
            data.CopyTo(frameData);
        }

        // PLC: record good frame for potential concealment
        _plc?.RecordGoodFrame(frameData);

        // Delay-based congestion control: track receive timing
        _delayController?.RecordFrame(timestamp, Environment.TickCount64);

        // Track for NACK
        _nackTracker?.RecordReceived(seq);

        // PLC: fill gaps with concealment frames for audio
        if (_plc != null && _lastReceivedSeq > 0)
        {
            for (uint missing = _lastReceivedSeq + 1; missing < seq; missing++)
            {
                // Try FEC first
                if (_fecDecoder != null && _fecDecoder.TryRecover(missing, missing, out var fecRecovered))
                {
                    _nackTracker?.RecordRetransmitReceived(missing);
                    DeliverFrame(new MediaFrameData(missing, 0, fecRecovered, false));
                    continue;
                }

                // FEC couldn't recover — use PLC concealment
                var concealed = _plc.GenerateConcealmentFrame();
                DeliverFrame(new MediaFrameData(missing, 0, concealed, false));
            }
        }
        else if (_fecDecoder != null && _lastReceivedSeq > 0)
        {
            // No PLC but have FEC — try recovery only
            for (uint missing = _lastReceivedSeq + 1; missing < seq; missing++)
            {
                if (_fecDecoder.TryRecover(missing, missing, out var recovered))
                {
                    _nackTracker?.RecordRetransmitReceived(missing);
                    DeliverFrame(new MediaFrameData(missing, 0, recovered, false));
                }
            }
        }

        // Register frame with FEC decoder for potential future recovery
        _fecDecoder?.AddFrame(seq, seq, data);

        _lastReceivedSeq = seq;

        // Deliver the actual frame
        DeliverFrame(new MediaFrameData(seq, timestamp, frameData, isKeyframe));
    }

    private void DeliverFrame(MediaFrameData frame)
    {
        if (_jitterBuffer != null)
        {
            _jitterBuffer.Enqueue(frame.SequenceNumber, frame.Timestamp, frame.Data, frame.IsKeyframe);
        }
        else
        {
            _inbound.Writer.TryWrite(frame);
        }
    }

    /// <summary>
    /// Read all incoming media frames as an async stream.
    /// If jitter buffer is enabled, frames are delivered after adaptive delay.
    /// </summary>
    public async IAsyncEnumerable<MediaFrameData> ReadFramesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (_jitterBuffer != null)
        {
            await foreach (var bf in _jitterBuffer.ReadAllAsync(ct))
                yield return new MediaFrameData(bf.SequenceNumber, bf.Timestamp, bf.Data, bf.IsKeyframe);
        }
        else
        {
            await foreach (var frame in _inbound.Reader.ReadAllAsync(ct))
                yield return frame;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _closed = true;
        _inbound.Writer.TryComplete();
        if (_nackTracker != null) await _nackTracker.DisposeAsync();
        if (_prober != null) await _prober.DisposeAsync();
        if (_jitterBuffer != null) await _jitterBuffer.DisposeAsync();
        if (_directManager != null) await _directManager.DisposeAsync();
        _encryption?.Dispose();
    }
}
