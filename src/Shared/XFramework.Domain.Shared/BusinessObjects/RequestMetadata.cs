
namespace XFramework.Domain.Shared.BusinessObjects;

[MemoryPackable]
public partial class RequestMetadata
{
    [MemoryPackOrder(0)]
    public Guid? RequestId { get; set; }

    [MemoryPackOrder(1)]
    public string? DeviceName { get; set; }

    [MemoryPackOrder(2)]
    public string? UserAgent { get; set; }

    [MemoryPackOrder(3)]
    public string? IpAddress { get; set; }

    [MemoryPackOrder(4)]
    public string? OperationName { get; set; }

    [MemoryPackOrder(5)]
    public Guid? RequestedTenantId { get; set; }
}
