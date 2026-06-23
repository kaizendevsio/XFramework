using XFramework.Domain.Shared.Attributes;

namespace XFramework.Inventario.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/inventario/product-variation-types",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "inventario-product-variation-types"
)]
public partial class ProductVariationType : BaseModel
{
    [MemoryPackOrder(0)]
    public string? Name { get; set; }
    [MemoryPackOrder(1)]
    public string? NormalizedName { get; set; }
    [MemoryPackOrder(2)]
    public string? Code { get; set; }
    [MemoryPackOrder(3)]
    public Guid? ProductId { get; set; }
    [MemoryPackIgnore]
    public Product? Product { get; set; }
}
