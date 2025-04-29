using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Blazor.Core.Features.Cart.Models;

public class CartItemVm : BaseModel
{
    public IProduct? Product { get; set; }
    public string? Name => Product?.Name;
    public string? Description => Product?.Description;
    public decimal Price => Product?.Price ?? 0;
    public decimal Total => Price * Quantity;
    public int Quantity { get; set; }

    public CartItemVm(IProduct product, int quantity = 1)
    {
        Product = product;
        Quantity = quantity;
        CreatedAt = DateTime.UtcNow; // Automatically set CreatedAt
        IsEnabled = true; // Automatically set IsEnabled
        Id = Guid.NewGuid(); // Generate a new ID for the cart item
    }
    public CartItemVm()
    {
        Quantity = 1; // Default quantity
        CreatedAt = DateTime.UtcNow; // Automatically set CreatedAt
        IsEnabled = true; // Automatically set IsEnabled
        Id = Guid.NewGuid();
    }
}