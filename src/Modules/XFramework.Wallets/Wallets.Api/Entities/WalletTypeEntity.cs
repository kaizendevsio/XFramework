using XFramework.Core.Attributes;

namespace XFramework.Wallets.Api.Entities;

/// <summary>
/// VSA wrapper entity for WalletType domain model.
/// Implements the VSA Wrapper Entity Pattern (ADR-003).
/// </summary>
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/wallet-types",
    RequireAuthorization = true,
    CacheDurationSeconds = 1800,
    CacheKeyPrefix = "wallet-types"
)]
public partial class WalletTypeEntity
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Desc { get; set; }
    public short Type { get; set; }
    public Guid? CurrencyTypeId { get; set; }
    public decimal? MinTransferRule { get; set; }
    public decimal? MaxTransferRule { get; set; }
    public decimal? BondBalanceRule { get; set; }
    public decimal? MaintainingBalanceRule { get; set; }
    public Guid SystemReferenceId { get; set; }
    
    // ISoftDeletable properties
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Request DTO for creating a WalletTypeEntity.
/// </summary>
public class CreateWalletTypeEntityRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Desc { get; set; }
    public short Type { get; set; }
    public Guid? CurrencyTypeId { get; set; }
    public decimal? MinTransferRule { get; set; }
    public decimal? MaxTransferRule { get; set; }
    public decimal? BondBalanceRule { get; set; }
    public decimal? MaintainingBalanceRule { get; set; }
    public Guid SystemReferenceId { get; set; }
}

/// <summary>
/// Request DTO for updating a WalletTypeEntity.
/// </summary>
public class UpdateWalletTypeEntityRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Desc { get; set; }
    public short Type { get; set; }
    public Guid? CurrencyTypeId { get; set; }
    public decimal? MinTransferRule { get; set; }
    public decimal? MaxTransferRule { get; set; }
    public decimal? BondBalanceRule { get; set; }
    public decimal? MaintainingBalanceRule { get; set; }
}

/// <summary>
/// Request DTO for listing WalletTypeEntities with pagination.
/// </summary>
public class GetWalletTypeEntityListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public short? Type { get; set; }
    public Guid? CurrencyTypeId { get; set; }
}