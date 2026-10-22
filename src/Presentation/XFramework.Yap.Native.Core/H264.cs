using System.Buffers.Binary;

namespace Yap.Native;

public static class H264
{
    public static List<byte[]> Nals(ReadOnlySpan<byte> data)
    {
        var result = new List<byte[]>(); int start = -1;
        for (var i = 0; i + 2 < data.Length; i++)
        {
            var prefix = data[i] == 0 && data[i + 1] == 0 ? (data[i + 2] == 1 ? 3 :
                i + 3 < data.Length && data[i + 2] == 0 && data[i + 3] == 1 ? 4 : 0) : 0;
            if (prefix == 0) continue;
            if (start >= 0 && i > start) result.Add(data[start..i].ToArray());
            start = i + prefix; i = start - 1;
        }
        if (start >= 0 && start < data.Length) result.Add(data[start..].ToArray());
        return result;
    }
    public static byte[] AnnexB(IEnumerable<byte[]> nals)
    {
        using var output = new MemoryStream();
        foreach (var nal in nals) { output.Write(new byte[] { 0, 0, 0, 1 }); output.Write(nal); }
        return output.ToArray();
    }
    public static byte[] Avcc(IEnumerable<byte[]> nals)
    {
        using var output = new MemoryStream(); var length = new byte[4];
        foreach (var nal in nals) { BinaryPrimitives.WriteInt32BigEndian(length, nal.Length); output.Write(length); output.Write(nal); }
        return output.ToArray();
    }
    public static List<byte[]> FromAvcc(ReadOnlySpan<byte> data)
    {
        var nals = new List<byte[]>();
        while (data.Length > 0)
        {
            if (data.Length < 4) throw new InvalidDataException("Truncated H264 length.");
            var count = BinaryPrimitives.ReadInt32BigEndian(data); data = data[4..];
            if (count <= 0 || count > data.Length) throw new InvalidDataException("Invalid H264 NAL length.");
            nals.Add(data[..count].ToArray()); data = data[count..];
        }
        return nals;
    }
}
