namespace XFramework.Inventario.Domain.Shared.Contracts;

using XFramework.Domain.Shared.Contracts.Base;

public class Product : BaseModel, IProduct
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public Guid CategoryId { get; set; }
    public ProductCategory? Category { get; set; }
    public List<ProductVariation>? Variations { get; set; } = new();
    public List<ProductTransaction>? Transactions { get; set; } = new();
    public string? Image { get; set; }
    public string? SKU { get; set; }
    public string? Brand { get; set; }
    public decimal? Weight { get; set; }
    public (string Length, string Width, string Height)? Dimensions { get; set; }
    public List<string>? Tags { get; set; } = new();
    public decimal? Rating { get; set; }
    public List<string>? Reviews { get; set; } = new();
    public decimal? Discount { get; set; }
    public bool IsAvailable { get; set; }
}