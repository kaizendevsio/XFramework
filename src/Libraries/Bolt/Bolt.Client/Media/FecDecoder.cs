namespace Bolt.Client.Media;

public sealed class FecDecoder
{
    private readonly Dictionary<uint, Dictionary<uint, byte[]>> _groups = new();
    private readonly Dictionary<uint, (byte[] Parity, int[] Lengths, byte Size)> _parityFrames = new();

    public void AddFrame(uint sequenceNumber, uint groupStart, ReadOnlyMemory<byte> data)
    {
        if (!_groups.TryGetValue(groupStart, out var group))
        {
            group = new Dictionary<uint, byte[]>();
            _groups[groupStart] = group;
        }
        group[sequenceNumber] = data.ToArray();
    }

    public void AddFecFrame(uint groupStart, byte groupSize, ReadOnlyMemory<byte> parityData, int[] originalLengths)
    {
        _parityFrames[groupStart] = (parityData.ToArray(), originalLengths, groupSize);
    }

    public bool TryRecover(uint missingSequenceNumber, uint groupStart, out byte[] recoveredData)
    {
        recoveredData = Array.Empty<byte>();
        if (!_parityFrames.TryGetValue(groupStart, out var fec)) return false;
        if (!_groups.TryGetValue(groupStart, out var group)) return false;
        if (group.Count < fec.Size - 1) return false;
        if (group.ContainsKey(missingSequenceNumber)) return false;

        var result = (byte[])fec.Parity.Clone();
        foreach (var (_, data) in group)
        {
            for (var i = 0; i < data.Length; i++)
                result[i] ^= data[i];
        }

        var idx = (int)(missingSequenceNumber - groupStart);
        recoveredData = idx >= 0 && idx < fec.Lengths.Length ? result[..fec.Lengths[idx]] : result;
        return true;
    }

    public void CleanupGroup(uint groupStart)
    {
        _groups.Remove(groupStart);
        _parityFrames.Remove(groupStart);
    }
}
