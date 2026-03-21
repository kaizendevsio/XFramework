using XFramework.Core.Attributes;

namespace XFramework.Inventario.Api.Entities;

/// <summary>
/// Test category entity to demonstrate the EntityServiceGenerator.
/// Categories can be used to organize products.
/// </summary>
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/testcategories",
    RequireAuthorization = true,
    CacheDurationSeconds = 600,
    CacheKeyPrefix = "testcategories"
)]
public partial class TestCategory
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Request DTO for creating a TestCategory.
/// </summary>
public class CreateTestCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Request DTO for updating a TestCategory.
/// </summary>
public class UpdateTestCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Request DTO for listing TestCategories with pagination.
/// </summary>
public class GetTestCategoryListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}