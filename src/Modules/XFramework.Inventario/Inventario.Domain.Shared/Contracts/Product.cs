using XFramework.Domain.Shared.Attributes;

namespace XFramework.Inventario.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[AllowRemoteDataContextMutation]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/inventario/products",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "inventario-products"
)]
public partial class Product : BaseModel, IProduct
{
    [MemoryPackOrder(0)]
    public string? Name { get; set; }
    [MemoryPackOrder(1)]
    public string? Description { get; set; }
    [MemoryPackOrder(2)]
    public decimal Price { get; set; }
    [MemoryPackOrder(3)]
    public int StockQuantity { get; set; }
    [MemoryPackOrder(4)]
    public Guid CategoryId { get; set; }
    [MemoryPackOrder(5)]
    public ProductCategory? Category { get; set; }
    [MemoryPackOrder(6)]
    public List<ProductVariation>? Variations { get; set; } = new();
    [MemoryPackOrder(7)]
    public List<ProductTransaction>? Transactions { get; set; } = new();
    [MemoryPackOrder(8)]
    public string? Image { get; set; }
    [MemoryPackOrder(9)]
    public string? SKU { get; set; }
    [MemoryPackOrder(10)]
    public string? Brand { get; set; }
    [MemoryPackOrder(11)]
    public decimal? Weight { get; set; }
    [MemoryPackOrder(12)]
    [MemoryPackIgnore]
    public (string Length, string Width, string Height)? Dimensions { get; set; }
    [MemoryPackOrder(13)]
    [MemoryPackIgnore]
    public List<string>? Tags { get; set; } = new();
    [MemoryPackOrder(14)]
    public decimal? Rating { get; set; }
    [MemoryPackOrder(15)]
    [MemoryPackIgnore]
    public List<string>? Reviews { get; set; } = new();
    [MemoryPackOrder(16)]
    public decimal? Discount { get; set; }
    [MemoryPackOrder(17)]
    public bool IsAvailable { get; set; }
}
