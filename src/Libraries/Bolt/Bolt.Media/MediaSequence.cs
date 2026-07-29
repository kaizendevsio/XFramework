namespace Bolt.Media;

internal static class MediaSequence
{
    private const uint HalfRange = 0x8000_0000;

    public static uint ForwardDistance(uint from, uint to) => unchecked(to - from);

    public static bool IsNewer(uint candidate, uint reference)
    {
        var distance = ForwardDistance(reference, candidate);
        return distance is > 0 and < HalfRange;
    }

    public static bool IsOlderThan(uint candidate, uint reference, uint maximumAge)
    {
        var age = ForwardDistance(candidate, reference);
        return age > maximumAge && age < HalfRange;
    }

    public static bool IsInGroup(uint sequenceNumber, uint groupStart, byte groupSize) =>
        ForwardDistance(groupStart, sequenceNumber) < groupSize;
}
