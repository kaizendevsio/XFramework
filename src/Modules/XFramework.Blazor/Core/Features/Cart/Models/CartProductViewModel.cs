using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Blazor.Core.Features.Cart.Models;

public class CartProductViewModel : BaseModel
{
    public Product? Product { get; set; }
    public string? Name => Product?.Name;
    public string? Description => Product?.Description;
    public decimal Price => Product?.Price ?? 0;
    public decimal Total => Price * Quantity;
    public int Quantity { get; set; }

    public CartProductViewModel(Product product, int quantity = 1)
    {
        Product = product;
        Quantity = quantity;
        CreatedAt = DateTime.UtcNow; // Automatically set CreatedAt
        IsEnabled = true; // Automatically set IsEnabled
    }
    public CartProductViewModel()
    {
        Product = new Product();
        Quantity = 1; // Default quantity
        CreatedAt = DateTime.UtcNow; // Automatically set CreatedAt
        IsEnabled = true; // Automatically set IsEnabled
    }
}