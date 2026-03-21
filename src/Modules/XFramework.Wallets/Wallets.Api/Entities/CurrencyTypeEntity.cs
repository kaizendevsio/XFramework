using XFramework.Core.Attributes;

namespace XFramework.Wallets.Api.Entities;

/// <summary>
/// VSA wrapper entity for CurrencyType domain model.
/// Implements the VSA Wrapper Entity Pattern (ADR-003).
/// </summary>
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/currencies",
    RequireAuthorization = true,
    CacheDurationSeconds = 3600,
    CacheKeyPrefix = "currencies"
)]
public partial class CurrencyTypeEntity
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? CurrencyIsoCode3 { get; set; }
    public string? Description { get; set; }
    public short? Type { get; set; }
    public Guid SystemReferenceId { get; set; }
    
    // ISoftDeletable properties
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Request DTO for creating a CurrencyTypeEntity.
/// </summary>
public class CreateCurrencyTypeEntityRequest
{
    public string Name { get; set; } = string.Empty;
    public string CurrencyIsoCode3 { get; set; } = string.Empty;
    public string? Description { get; set; }
    public short? Type { get; set; }
    public Guid SystemReferenceId { get; set; }
}

/// <summary>
/// Request DTO for updating a CurrencyTypeEntity.
/// </summary>
public class UpdateCurrencyTypeEntityRequest
{
    public string Name { get; set; } = string.Empty;
    public string CurrencyIsoCode3 { get; set; } = string.Empty;
    public string? Description { get; set; }
    public short? Type { get; set; }
}

/// <summary>
/// Request DTO for listing CurrencyTypeEntities with pagination.
/// </summary>
public class GetCurrencyTypeEntityListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public short? Type { get; set; }
}