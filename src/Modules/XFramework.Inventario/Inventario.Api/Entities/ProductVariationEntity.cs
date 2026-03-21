using XFramework.Core.Attributes;

namespace XFramework.Inventario.Api.Entities;

/// <summary>
/// VSA wrapper entity for ProductVariation domain model.
/// Implements the VSA Wrapper Entity Pattern (ADR-003).
/// </summary>
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/productvariations",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "productvariations"
)]
public partial class ProductVariationEntity
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public decimal AdditionalPrice { get; set; }
    public Guid ProductId { get; set; }
    
    // ISoftDeletable properties
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Request DTO for creating a ProductVariationEntity.
/// </summary>
public class CreateProductVariationEntityRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal AdditionalPrice { get; set; }
    public Guid ProductId { get; set; }
}

/// <summary>
/// Request DTO for updating a ProductVariationEntity.
/// </summary>
public class UpdateProductVariationEntityRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal AdditionalPrice { get; set; }
    public Guid ProductId { get; set; }
}

/// <summary>
/// Request DTO for listing ProductVariationEntities with pagination.
/// </summary>
public class GetProductVariationEntityListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public Guid? ProductId { get; set; }
    public string? SearchTerm { get; set; }
}