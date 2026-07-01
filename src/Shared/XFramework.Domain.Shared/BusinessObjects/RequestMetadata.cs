
namespace XFramework.Domain.Shared.BusinessObjects;

[MemoryPackable]
public partial class RequestMetadata
{
    [MemoryPackOrder(0)]
    public Guid? SessionId { get; set; }

    [MemoryPackOrder(1)]
    public Guid? TenantId { get; set; }

    [MemoryPackOrder(2)]
    public Guid? CredentialId { get; set; }

    [MemoryPackOrder(3)]
    public string? Name { get; set; }

    [MemoryPackOrder(4)]
    public string? DeviceName { get; set; }

    [MemoryPackOrder(5)]
    public string? DeviceAgent { get; set; }

    [MemoryPackOrder(6)]
    public string? IpAddress { get; set; }

    [MemoryPackOrder(7)]
    public Guid? RequestId { get; set; }

    [MemoryPackOrder(8)]
    public string? ActorAccessToken { get; set; }

    [MemoryPackOrder(9)]
    public string? ServiceAccessToken { get; set; }
}
