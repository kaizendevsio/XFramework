using XFramework.Core.Attributes;

namespace XFramework.Wallets.Api.Entities;

/// <summary>
/// VSA wrapper entity for Wallet domain model.
/// Implements the VSA Wrapper Entity Pattern (ADR-003).
/// Note: This entity is READ-ONLY for generated endpoints. Use IWalletService for balance modifications.
/// </summary>
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.Get | EndpointActions.GetList,
    RoutePrefix = "api/wallets",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "wallets"
)]
public partial class WalletEntity
{
    public Guid Id { get; set; }
    public Guid CredentialId { get; set; }
    public Guid? WalletTypeId { get; set; }
    public decimal Balance { get; set; }
    public string? AccountNumber { get; set; }
    public int CardNumber { get; set; }
    public decimal DebitOnHoldBalance { get; set; }
    public decimal CreditOnHoldBalance { get; set; }
    public decimal TransferableBalance { get; set; }
    public decimal? MinTransferRule { get; set; }
    public decimal? MaxTransferRule { get; set; }
    public decimal? BondBalanceRule { get; set; }
    public decimal? MaintainingBalanceRule { get; set; }
    
    // Computed properties (stored for read operations)
    public decimal? TotalBalance { get; set; }
    public decimal? AvailableBalance { get; set; }
    
    // ISoftDeletable properties
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Request DTO for listing WalletEntities with pagination.
/// Note: Create and Update operations should use IWalletService.
/// </summary>
public class GetWalletEntityListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? CredentialId { get; set; }
    public Guid? WalletTypeId { get; set; }
    public decimal? MinBalance { get; set; }
    public decimal? MaxBalance { get; set; }
}