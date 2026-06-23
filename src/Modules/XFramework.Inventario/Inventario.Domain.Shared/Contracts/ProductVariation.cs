using XFramework.Domain.Shared.Attributes;

namespace XFramework.Inventario.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
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
    [MemoryPackOrder(4)]
    public string? VariationType { get; set; }
    [MemoryPackOrder(5)]
    public Guid? ProductVariationTypeId { get; set; }
    [MemoryPackIgnore]
    public ProductVariationType? ProductVariationType { get; set; }
    [MemoryPackOrder(6)]
    public decimal Price { get; set; }
}
