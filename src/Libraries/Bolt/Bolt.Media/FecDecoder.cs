namespace Bolt.Media;

public sealed class FecDecoder
{
    private const int DefaultMaximumTrackedFrames = 512;
    private const int DefaultMaximumGroups = 32;

    private readonly Dictionary<uint, TrackedFrame> _frames = [];
    private readonly Dictionary<uint, FecGroup> _groups = [];
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _retention;
    private readonly int _maximumTrackedFrames;
    private readonly int _maximumGroups;

    public FecDecoder(
        TimeProvider? timeProvider = null,
        TimeSpan? retention = null,
        int maximumTrackedFrames = DefaultMaximumTrackedFrames,
        int maximumGroups = DefaultMaximumGroups)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumTrackedFrames, FecEncoder.MaximumGroupSize);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumGroups, 1);

        _timeProvider = timeProvider ?? TimeProvider.System;
        _retention = retention ?? TimeSpan.FromSeconds(2);
        if (_retention <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(retention));

        _maximumTrackedFrames = maximumTrackedFrames;
        _maximumGroups = maximumGroups;
    }

    public void AddFrame(uint sequenceNumber, ReadOnlyMemory<byte> data)
    {
        PruneExpired();
        _frames[sequenceNumber] = new TrackedFrame(data.ToArray(), _timeProvider.GetUtcNow());
        TrimOldest(_frames, _maximumTrackedFrames, static frame => frame.CreatedAt);
    }

    // Retained for source compatibility. Group membership is derived from parity metadata.
    public void AddFrame(uint sequenceNumber, uint groupStart, ReadOnlyMemory<byte> data) =>
        AddFrame(sequenceNumber, data);

    public void AddFecFrame(
        uint groupStart,
        byte groupSize,
        ReadOnlyMemory<byte> parityData,
        int[] originalLengths)
    {
        PruneExpired();

        if (groupSize is < 2 or > FecEncoder.MaximumGroupSize ||
            originalLengths.Length != groupSize ||
            originalLengths.Any(length => length < 0 || length > parityData.Length))
        {
            return;
        }

        _groups[groupStart] = new FecGroup(
            parityData.ToArray(),
            originalLengths.ToArray(),
            groupSize,
            _timeProvider.GetUtcNow());
        TrimOldest(_groups, _maximumGroups, static group => group.CreatedAt);
    }

    public bool TryRecover(uint missingSequenceNumber, uint groupStart, out byte[] recoveredData)
    {
        recoveredData = [];
        PruneExpired();

        if (!_groups.TryGetValue(groupStart, out var fec) ||
            !MediaSequence.IsInGroup(missingSequenceNumber, groupStart, fec.Size) ||
            _frames.ContainsKey(missingSequenceNumber))
        {
            return false;
        }

        var missingCount = 0;
        for (uint offset = 0; offset < fec.Size; offset++)
        {
            var sequence = unchecked(groupStart + offset);
            if (!_frames.ContainsKey(sequence))
                missingCount++;
        }

        if (missingCount != 1)
            return false;

        var recovered = fec.Parity.ToArray();
        for (uint offset = 0; offset < fec.Size; offset++)
        {
            var sequence = unchecked(groupStart + offset);
            if (sequence == missingSequenceNumber)
                continue;

            if (!_frames.TryGetValue(sequence, out var frame))
                return false;

            for (var index = 0; index < Math.Min(frame.Data.Length, recovered.Length); index++)
                recovered[index] ^= frame.Data[index];
        }

        var missingIndex = (int)MediaSequence.ForwardDistance(groupStart, missingSequenceNumber);
        recoveredData = recovered[..fec.Lengths[missingIndex]];
        CleanupGroup(groupStart, fec.Size);
        return true;
    }

    public bool TryRecoverSingle(uint groupStart, out uint sequenceNumber, out byte[] recoveredData)
    {
        sequenceNumber = 0;
        recoveredData = [];
        PruneExpired();

        if (!_groups.TryGetValue(groupStart, out var fec))
            return false;

        uint? missing = null;
        for (uint offset = 0; offset < fec.Size; offset++)
        {
            var candidate = unchecked(groupStart + offset);
            if (_frames.ContainsKey(candidate))
                continue;

            if (missing.HasValue)
                return false;

            missing = candidate;
        }

        if (!missing.HasValue || !TryRecover(missing.Value, groupStart, out recoveredData))
            return false;

        sequenceNumber = missing.Value;
        return true;
    }

    public void CleanupGroup(uint groupStart)
    {
        if (_groups.TryGetValue(groupStart, out var group))
            CleanupGroup(groupStart, group.Size);
    }

    private void CleanupGroup(uint groupStart, byte groupSize)
    {
        _groups.Remove(groupStart);
        for (uint offset = 0; offset < groupSize; offset++)
            _frames.Remove(unchecked(groupStart + offset));
    }

    private void PruneExpired()
    {
        var cutoff = _timeProvider.GetUtcNow() - _retention;
        foreach (var sequence in _frames.Where(pair => pair.Value.CreatedAt <= cutoff).Select(pair => pair.Key).ToArray())
            _frames.Remove(sequence);
        foreach (var groupStart in _groups.Where(pair => pair.Value.CreatedAt <= cutoff).Select(pair => pair.Key).ToArray())
            _groups.Remove(groupStart);
    }

    private static void TrimOldest<T>(Dictionary<uint, T> values, int maximum, Func<T, DateTimeOffset> getCreatedAt)
    {
        while (values.Count > maximum)
        {
            var oldest = values.MinBy(pair => getCreatedAt(pair.Value)).Key;
            values.Remove(oldest);
        }
    }

    private sealed record TrackedFrame(byte[] Data, DateTimeOffset CreatedAt);
    private sealed record FecGroup(byte[] Parity, int[] Lengths, byte Size, DateTimeOffset CreatedAt);
}
