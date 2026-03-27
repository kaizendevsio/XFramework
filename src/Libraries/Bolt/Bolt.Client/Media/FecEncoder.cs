namespace Bolt.Client.Media;

public record FecResult(uint GroupStartSequence, byte GroupSize, byte[] ParityData, int[] OriginalLengths);

public sealed class FecEncoder
{
    private readonly int _groupSize;
    private readonly List<(uint Seq, byte[] Data)> _frames = new();

    public FecEncoder(int groupSize = 4) => _groupSize = groupSize;

    public FecResult? AddFrame(uint sequenceNumber, ReadOnlyMemory<byte> data)
    {
        _frames.Add((sequenceNumber, data.ToArray()));
        if (_frames.Count < _groupSize) return null;

        var maxLen = _frames.Max(f => f.Data.Length);
        var parity = new byte[maxLen];
        var lengths = new int[_groupSize];

        for (var i = 0; i < _groupSize; i++)
        {
            lengths[i] = _frames[i].Data.Length;
            for (var j = 0; j < _frames[i].Data.Length; j++)
                parity[j] ^= _frames[i].Data[j];
        }

        var result = new FecResult(_frames[0].Seq, (byte)_groupSize, parity, lengths);
        _frames.Clear();
        return result;
    }
}
