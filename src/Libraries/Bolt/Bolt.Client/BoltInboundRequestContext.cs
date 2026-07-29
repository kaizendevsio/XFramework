namespace Bolt.Client;

/// <summary>
/// Immutable routing context for an inbound Bolt request.
/// </summary>
public readonly record struct BoltInboundRequestContext(Guid RequestId, int SenderHash);
