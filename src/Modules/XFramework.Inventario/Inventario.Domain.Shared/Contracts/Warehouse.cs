using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.Attributes;

namespace XFramework.Inventario.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/inventario/warehouses",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "inventario-warehouses"
)]
public partial class Warehouse : BaseModel
{
    [MemoryPackOrder(0)]
    public string Code { get; set; } = string.Empty;
    [MemoryPackOrder(1)]
    public string Name { get; set; } = string.Empty;
    [MemoryPackOrder(2)]
    public string? Description { get; set; }
    [MemoryPackOrder(3)]
    public string? AddressLine { get; set; }
    [MemoryPackOrder(4)]
    public string? City { get; set; }
    [MemoryPackOrder(5)]
    public string? Region { get; set; }
    [MemoryPackOrder(6)]
    public string? PostalCode { get; set; }
    [MemoryPackOrder(7)]
    public string? CountryCode { get; set; }
    [MemoryPackOrder(8)]
    public bool IsDefault { get; set; }
    [MemoryPackIgnore]
    public List<InventoryLocation> Locations { get; set; } = new();
}
