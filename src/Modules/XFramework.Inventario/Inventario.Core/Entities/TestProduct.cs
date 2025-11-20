using XFramework.Core.Attributes;

namespace XFramework.Inventario.Core.Entities;

/// <summary>
/// Test entity to verify the EntityServiceGenerator works correctly.
/// This entity has all CRUD operations enabled.
/// </summary>
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/testproducts",
    RequireAuthorization = true,
    CacheDurationSeconds = 600,
    CacheKeyPrefix = "testproducts"
)]
public partial class TestProduct
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Request DTO for creating a TestProduct.
/// </summary>
public class CreateTestProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
}

/// <summary>
/// Request DTO for updating a TestProduct.
/// </summary>
public class UpdateTestProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
}

/// <summary>
/// Request DTO for listing TestProducts with pagination.
/// </summary>
public class GetTestProductListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}