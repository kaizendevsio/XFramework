namespace Bolt.Protocol;

/// <summary>Connection-local control commands carried by ordinary RPC frames.</summary>
public static class BoltConnectionControl
{
    public const string GetExpiry = "__bolt_connection_expiry__";
    public const string Drain = "__bolt_connection_drain__";
    public static readonly int GetExpiryHash = BoltCodec.Fnv1aHash(GetExpiry);
    public static readonly int DrainHash = BoltCodec.Fnv1aHash(Drain);
}
