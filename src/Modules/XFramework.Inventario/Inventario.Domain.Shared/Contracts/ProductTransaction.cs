namespace XFramework.Inventario.Domain.Shared.Contracts;

using XFramework.Domain.Shared.Contracts.Base;

public class ProductTransaction : BaseModel
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
    public decimal TotalPrice { get; set; }
    public DateTime TransactionDate { get; set; }
}