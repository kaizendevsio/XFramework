namespace XFramework.Inventario.Domain.Shared.Contracts;

using XFramework.Domain.Shared.Contracts.Base;

public class Product : BaseModel
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public Guid CategoryId { get; set; }
    public ProductCategory? Category { get; set; }
    public List<ProductVariation>? Variations { get; set; } = new();
    public List<ProductTransaction>? Transactions { get; set; } = new();
}