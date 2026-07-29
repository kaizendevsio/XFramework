using System.Buffers;

namespace Bolt.Protocol.Buffers;

/// <summary>
/// A MemoryManager that wraps an ArrayPool-rented buffer.
/// Use this instead of GC.AllocateUninitializedArray / new byte[] when you need a
/// ReadOnlyMemory&lt;byte&gt; whose lifetime is scoped to a consumer. The owner must be
/// disposed deterministically after the consumer finishes with the memory.
///
/// Allocation cost: ~32 bytes (object header) vs 512KB+ for the payload byte[].
/// The payload byte[] comes from ArrayPool and is recycled, not GC-tracked.
/// </summary>
public sealed class PooledMemoryOwner : MemoryManager<byte>
{
    private byte[]? _array;
    private readonly int _length;

    public PooledMemoryOwner(int length)
    {
        _array = ArrayPool<byte>.Shared.Rent(length);
        _length = length;
    }

    /// <summary>Writable span for filling the buffer before handing out Memory.</summary>
    public Span<byte> WritableSpan => _array!.AsSpan(0, _length);

    /// <summary>Exact-length Memory backed by this pooled buffer.</summary>
    public override Memory<byte> Memory => CreateMemory(0, _length);

    public override Span<byte> GetSpan() => _array!.AsSpan(0, _length);

    protected override bool TryGetArray(out ArraySegment<byte> segment)
    {
        if (_array is not null)
        {
            segment = new ArraySegment<byte>(_array, 0, _length);
            return true;
        }
        segment = default;
        return false;
    }

    public override MemoryHandle Pin(int elementIndex = 0) =>
        throw new NotSupportedException("PooledMemoryOwner does not support pinning.");

    public override void Unpin() { }

    protected override void Dispose(bool disposing)
    {
        var arr = Interlocked.Exchange(ref _array, null);
        if (arr is not null)
            ArrayPool<byte>.Shared.Return(arr);
    }
}
