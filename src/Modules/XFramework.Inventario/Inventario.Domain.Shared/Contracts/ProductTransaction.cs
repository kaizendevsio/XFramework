using XFramework.Domain.Shared.Attributes;

namespace XFramework.Inventario.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/inventario/product-transactions",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "inventario-product-transactions"
)]
public partial class ProductTransaction : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid ProductId { get; set; }
    [MemoryPackOrder(1)]
    public Product? Product { get; set; }
    [MemoryPackOrder(2)]
    public int Quantity { get; set; }
    [MemoryPackOrder(3)]
    public decimal TotalPrice { get; set; }
    [MemoryPackOrder(4)]
    public DateTime TransactionDate { get; set; }
}
