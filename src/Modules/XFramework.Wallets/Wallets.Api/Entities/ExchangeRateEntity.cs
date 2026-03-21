using XFramework.Core.Attributes;

namespace XFramework.Wallets.Api.Entities;

/// <summary>
/// VSA wrapper entity for ExchangeRate domain model.
/// Implements the VSA Wrapper Entity Pattern (ADR-003).
/// </summary>
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/exchange-rates",
    RequireAuthorization = true,
    CacheDurationSeconds = 1800,
    CacheKeyPrefix = "exchange-rates"
)]
public partial class ExchangeRateEntity
{
    public Guid Id { get; set; }
    public Guid SourceCurrencyTypeId { get; set; }
    public Guid TargetCurrencyTypeId { get; set; }
    public decimal? Value { get; set; }
    public decimal? Fee { get; set; }
    public DateTime? EffectivityDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    
    // ISoftDeletable properties
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Request DTO for creating an ExchangeRateEntity.
/// </summary>
public class CreateExchangeRateEntityRequest
{
    public Guid SourceCurrencyTypeId { get; set; }
    public Guid TargetCurrencyTypeId { get; set; }
    public decimal? Value { get; set; }
    public decimal? Fee { get; set; }
    public DateTime? EffectivityDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
}

/// <summary>
/// Request DTO for updating an ExchangeRateEntity.
/// </summary>
public class UpdateExchangeRateEntityRequest
{
    public decimal? Value { get; set; }
    public decimal? Fee { get; set; }
    public DateTime? EffectivityDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
}

/// <summary>
/// Request DTO for listing ExchangeRateEntities with pagination.
/// </summary>
public class GetExchangeRateEntityListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public Guid? SourceCurrencyTypeId { get; set; }
    public Guid? TargetCurrencyTypeId { get; set; }
    public DateTime? EffectiveOn { get; set; }
}