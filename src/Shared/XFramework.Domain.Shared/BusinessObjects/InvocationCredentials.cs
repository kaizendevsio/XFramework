namespace XFramework.Domain.Shared.BusinessObjects;

public sealed record InvocationCredentials(
    string? ActorAccessToken,
    string? ServiceAccessToken);

[MemoryPackable]
public partial record BoltInvocationEnvelope
{
    [MemoryPackOrder(0)]
    public byte[] Payload { get; set; } = [];

    [MemoryPackOrder(1)]
    public string? ActorAccessToken { get; set; }

    [MemoryPackOrder(2)]
    public string? ServiceAccessToken { get; set; }
}
