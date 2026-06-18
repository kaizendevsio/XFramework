using XFramework.Domain.Shared.Attributes;

namespace XFramework.Inventario.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/inventario/product-categories",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "inventario-product-categories"
)]
public partial class ProductCategory : BaseModel
{
    [MemoryPackOrder(0)]
    public string? Name { get; set; }
    [MemoryPackOrder(1)]
    public string? Description { get; set; }
    [MemoryPackOrder(2)]
    public List<Product>? Products { get; set; } = new();
}
