using XFramework.Domain.Shared.Attributes;

namespace XFramework.Inventario.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[AllowRemoteDataContextMutation]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/inventario/product-variations",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "inventario-product-variations"
)]
public partial class ProductVariation : BaseModel
{
    [MemoryPackOrder(0)]
    public string? Name { get; set; }
    [MemoryPackOrder(1)]
    public decimal AdditionalPrice { get; set; }
    [MemoryPackOrder(2)]
    public Guid ProductId { get; set; }
    [MemoryPackOrder(3)]
    public Product? Product { get; set; }
}
