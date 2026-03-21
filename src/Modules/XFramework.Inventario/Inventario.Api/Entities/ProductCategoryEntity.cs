using XFramework.Core.Attributes;

namespace XFramework.Inventario.Api.Entities;

/// <summary>
/// VSA wrapper entity for ProductCategory domain model.
/// Implements the VSA Wrapper Entity Pattern (ADR-003).
/// </summary>
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/productcategories",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "productcategories"
)]
public partial class ProductCategoryEntity
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    
    // ISoftDeletable properties
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Request DTO for creating a ProductCategoryEntity.
/// </summary>
public class CreateProductCategoryEntityRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// Request DTO for updating a ProductCategoryEntity.
/// </summary>
public class UpdateProductCategoryEntityRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// Request DTO for listing ProductCategoryEntities with pagination.
/// </summary>
public class GetProductCategoryEntityListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
}