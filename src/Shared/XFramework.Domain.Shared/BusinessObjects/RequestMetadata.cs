
namespace XFramework.Domain.Shared.BusinessObjects;

[MemoryPackable]
public partial class RequestMetadata
{
    public Guid? SessionId { get; set; }
    public Guid? TenantId { get; set; }
    public string? Name { get; set; }
    public string? DeviceName { get; set; }
    public string? DeviceAgent { get; set; }
    public string? IpAddress { get; set; }
    public Guid? RequestId { get; set; }
}