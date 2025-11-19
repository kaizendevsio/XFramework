using XFramework.Inventario.Domain.Shared.Contracts;

namespace Inventario.Api.Features.Products;

/// <summary>
/// Response DTO for Product
/// </summary>
public class ProductResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public Guid CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? SKU { get; set; }
    public string? Brand { get; set; }
    public decimal? Weight { get; set; }
    public string? Image { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }

    public static ProductResponse FromProduct(Product product)
    {
        return new ProductResponse
        {
            Id = product.Id,
            Name = product.Name ?? string.Empty,
            Description = product.Description,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            CategoryId = product.CategoryId,
            CategoryName = product.Category?.Name,
            SKU = product.SKU,
            Brand = product.Brand,
            Weight = product.Weight,
            Image = product.Image,
            IsAvailable = product.IsAvailable,
            CreatedAt = product.CreatedAt,
            ModifiedAt = product.ModifiedAt
        };
    }
}

/// <summary>
/// Paginated response for products
/// </summary>
public class PaginatedProductResponse
{
    public List<ProductResponse> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasPrevious { get; set; }
    public bool HasNext { get; set; }
}