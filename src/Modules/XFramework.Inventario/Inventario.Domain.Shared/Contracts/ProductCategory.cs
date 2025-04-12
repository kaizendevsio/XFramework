namespace XFramework.Inventario.Domain.Shared.Contracts;

using XFramework.Domain.Shared.Contracts.Base;

public class ProductCategory : BaseModel
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<Product>? Products { get; set; } = new();
}