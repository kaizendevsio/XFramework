using XFramework.Core.Attributes;

namespace XFramework.Inventario.Api.Entities;

/// <summary>
/// VSA wrapper entity for Product domain model.
/// Implements the VSA Wrapper Entity Pattern (ADR-003).
/// </summary>
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/products",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "products"
)]
public partial class ProductEntity
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public Guid CategoryId { get; set; }
    public string? Image { get; set; }
    public string? SKU { get; set; }
    public string? Brand { get; set; }
    public decimal? Weight { get; set; }
    
    // Flattened dimensions (from tuple in domain model)
    public string? DimensionsLength { get; set; }
    public string? DimensionsWidth { get; set; }
    public string? DimensionsHeight { get; set; }
    
    public List<string>? Tags { get; set; } = new();
    public decimal? Rating { get; set; }
    public List<string>? Reviews { get; set; } = new();
    public decimal? Discount { get; set; }
    public bool IsAvailable { get; set; }
    
    // ISoftDeletable properties
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Request DTO for creating a ProductEntity.
/// </summary>
public class CreateProductEntityRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public Guid CategoryId { get; set; }
    public string? Image { get; set; }
    public string? SKU { get; set; }
    public string? Brand { get; set; }
    public decimal? Weight { get; set; }
    public string? DimensionsLength { get; set; }
    public string? DimensionsWidth { get; set; }
    public string? DimensionsHeight { get; set; }
    public List<string>? Tags { get; set; }
    public decimal? Rating { get; set; }
    public List<string>? Reviews { get; set; }
    public decimal? Discount { get; set; }
    public bool IsAvailable { get; set; }
}

/// <summary>
/// Request DTO for updating a ProductEntity.
/// </summary>
public class UpdateProductEntityRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public Guid CategoryId { get; set; }
    public string? Image { get; set; }
    public string? SKU { get; set; }
    public string? Brand { get; set; }
    public decimal? Weight { get; set; }
    public string? DimensionsLength { get; set; }
    public string? DimensionsWidth { get; set; }
    public string? DimensionsHeight { get; set; }
    public List<string>? Tags { get; set; }
    public decimal? Rating { get; set; }
    public List<string>? Reviews { get; set; }
    public decimal? Discount { get; set; }
    public bool IsAvailable { get; set; }
}

/// <summary>
/// Request DTO for listing ProductEntities with pagination.
/// </summary>
public class GetProductEntityListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? CategoryId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool? IsAvailable { get; set; }
}