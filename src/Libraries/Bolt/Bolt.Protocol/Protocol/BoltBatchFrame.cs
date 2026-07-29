using System.Buffers.Binary;

namespace Bolt.Protocol;

public readonly ref struct BoltBatchFrame
{
    private readonly ReadOnlySpan<byte> _entries;

    internal BoltBatchFrame(ReadOnlySpan<byte> entries, int count)
    {
        _entries = entries;
        Count = count;
    }

    public int Count { get; }

    public Enumerator GetEnumerator() => new(_entries, Count);

    public ref struct Enumerator
    {
        private readonly ReadOnlySpan<byte> _entries;
        private int _remaining;
        private int _offset;

        internal Enumerator(ReadOnlySpan<byte> entries, int count)
        {
            _entries = entries;
            _remaining = count;
            _offset = 0;
            Current = default;
        }

        public ReadOnlySpan<byte> Current { get; private set; }

        public bool MoveNext()
        {
            if (_remaining == 0)
                return false;

            var length = BinaryPrimitives.ReadInt32LittleEndian(_entries.Slice(_offset));
            _offset += 4;
            Current = _entries.Slice(_offset, length);
            _offset += length;
            _remaining--;
            return true;
        }
    }
}
