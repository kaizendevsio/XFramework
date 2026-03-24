using System.Buffers;
using System.Runtime.CompilerServices;

namespace Bolt.Protocol.Buffers;

/// <summary>
/// An IBufferWriter backed directly by ArrayPool. When complete, the caller
/// owns the rented array and must return it to the pool.
/// Eliminates the copy step that ArrayBufferWriter requires (write → copy → rent).
/// </summary>
public sealed class RentedBufferWriter : IBufferWriter<byte>, IDisposable
{
    private byte[] _buffer;
    private int _written;

    [ThreadStatic]
    private static RentedBufferWriter? _tls;

    /// <summary>
    /// Get a thread-local instance. Call Reset() before use and Complete() when done.
    /// </summary>
    public static RentedBufferWriter GetThreadLocal()
    {
        var writer = _tls ??= new RentedBufferWriter(512);
        writer.Reset();
        return writer;
    }

    public RentedBufferWriter(int initialCapacity)
    {
        _buffer = ArrayPool<byte>.Shared.Rent(initialCapacity);
        _written = 0;
    }

    public int WrittenCount => _written;

    public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _written);

    public ReadOnlyMemory<byte> WrittenMemory => _buffer.AsMemory(0, _written);

    /// <summary>
    /// Detach the rented buffer. Caller takes ownership and must return to ArrayPool.
    /// Returns (buffer, writtenCount). Use buffer.AsMemory(0, writtenCount) for the data.
    /// </summary>
    public (byte[] Buffer, int WrittenCount) Detach()
    {
        var result = (_buffer, _written);
        _buffer = ArrayPool<byte>.Shared.Rent(512); // Get a new buffer for reuse
        _written = 0;
        return result;
    }

    public void Reset()
    {
        _written = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        _written += count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsMemory(_written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsSpan(_written);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void EnsureCapacity(int sizeHint)
    {
        if (sizeHint <= 0) sizeHint = 256;

        if (_written + sizeHint <= _buffer.Length)
            return;

        var newSize = Math.Max(_buffer.Length * 2, _written + sizeHint);
        var newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
        _buffer.AsSpan(0, _written).CopyTo(newBuffer);
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = newBuffer;
    }

    public void Dispose()
    {
        if (_buffer is not null)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = null!;
        }
    }
}
