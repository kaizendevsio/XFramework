using XFramework.Core.Attributes;

namespace XFramework.Wallets.Api.Entities;

/// <summary>
/// VSA wrapper entity for WalletTransactionLineItem domain model.
/// Implements the VSA Wrapper Entity Pattern (ADR-003).
/// </summary>
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/wallet-transaction-line-items",
    RequireAuthorization = true,
    CacheDurationSeconds = 600,
    CacheKeyPrefix = "wallet-transaction-line-items"
)]
public partial class WalletTransactionLineItemEntity
{
    public Guid Id { get; set; }
    public decimal? Amount { get; set; }
    public decimal Fee { get; set; }
    public string? Description { get; set; }
    public Guid WalletTransferId { get; set; }
    
    // ISoftDeletable properties
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Request DTO for creating a WalletTransactionLineItemEntity.
/// </summary>
public class CreateWalletTransactionLineItemEntityRequest
{
    public decimal? Amount { get; set; }
    public decimal Fee { get; set; }
    public string? Description { get; set; }
    public Guid WalletTransferId { get; set; }
}

/// <summary>
/// Request DTO for updating a WalletTransactionLineItemEntity.
/// </summary>
public class UpdateWalletTransactionLineItemEntityRequest
{
    public decimal? Amount { get; set; }
    public decimal Fee { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Request DTO for listing WalletTransactionLineItemEntities with pagination.
/// </summary>
public class GetWalletTransactionLineItemEntityListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? WalletTransferId { get; set; }
}