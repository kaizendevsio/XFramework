namespace Bolt.Media;

/// <summary>
/// Ring buffer that stores the last N sent media frames for NACK-based retransmission.
/// When a receiver reports missing sequence numbers, the sender looks them up here.
///
/// Thread-safe for concurrent write (sender) and read (NACK handler).
/// </summary>
public sealed class RetransmitBuffer
{
    private readonly BufferedSentFrame[] _buffer;
    private readonly int _capacity;
    private readonly object _lock = new();

    public RetransmitBuffer(int capacity = 256)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
        _buffer = new BufferedSentFrame[capacity];
    }

    /// <summary>
    /// Store a sent frame for potential retransmission.
    /// Overwrites the oldest entry when full (ring buffer).
    /// </summary>
    public void Store(uint sequenceNumber, uint timestamp, byte flags, ReadOnlyMemory<byte> payload)
    {
        var index = (int)(sequenceNumber % _capacity);
        lock (_lock)
        {
            _buffer[index] = new BufferedSentFrame(sequenceNumber, timestamp, flags, payload.ToArray());
        }
    }

    /// <summary>
    /// Try to retrieve a previously sent frame by sequence number.
    /// Returns false if the frame has been evicted from the ring buffer.
    /// </summary>
    public bool TryGet(uint sequenceNumber, out BufferedSentFrame frame)
    {
        var index = (int)(sequenceNumber % _capacity);
        lock (_lock)
        {
            frame = _buffer[index];
            return frame.Payload != null && frame.SequenceNumber == sequenceNumber;
        }
    }
}

/// <summary>A sent media frame stored for retransmission.</summary>
public readonly record struct BufferedSentFrame(uint SequenceNumber, uint Timestamp, byte Flags, byte[]? Payload);
