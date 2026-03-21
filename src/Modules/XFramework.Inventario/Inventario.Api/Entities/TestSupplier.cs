using XFramework.Core.Attributes;

namespace XFramework.Inventario.Api.Entities;

/// <summary>
/// Test supplier entity to demonstrate the EntityServiceGenerator with more fields.
/// Suppliers provide products to the inventory system.
/// </summary>
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/testsuppliers",
    RequireAuthorization = true,
    CacheDurationSeconds = 600,
    CacheKeyPrefix = "testsuppliers"
)]
public partial class TestSupplier
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Website { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Request DTO for creating a TestSupplier.
/// </summary>
public class CreateTestSupplierRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Website { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Request DTO for updating a TestSupplier.
/// </summary>
public class UpdateTestSupplierRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Website { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Request DTO for listing TestSuppliers with pagination.
/// </summary>
public class GetTestSupplierListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}