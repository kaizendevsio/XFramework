using XFramework.Domain.Shared.Attributes;
using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Inventario.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/inventario/suppliers",
    RequireAuthorization = true,
    CacheDurationSeconds = 60,
    CacheKeyPrefix = "inventario-suppliers"
)]
public partial class Supplier : BaseModel
{
    [MemoryPackOrder(0)]
    public string Code { get; set; } = string.Empty;
    [MemoryPackOrder(1)]
    public string Name { get; set; } = string.Empty;
    [MemoryPackOrder(2)]
    public string? ContactName { get; set; }
    [MemoryPackOrder(3)]
    public string? Email { get; set; }
    [MemoryPackOrder(4)]
    public string? Phone { get; set; }
    [MemoryPackOrder(5)]
    public bool IsActive { get; set; } = true;
}
