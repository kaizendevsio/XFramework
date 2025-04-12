namespace XFramework.Inventario.Domain.Shared.Contracts;

using XFramework.Domain.Shared.Contracts.Base;

public class ProductVariation : BaseModel
{
    public string? Name { get; set; }
    public decimal AdditionalPrice { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
}