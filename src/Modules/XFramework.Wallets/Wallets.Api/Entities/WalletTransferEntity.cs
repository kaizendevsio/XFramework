using XFramework.Core.Attributes;
using XFramework.Domain.Shared.Enums;

namespace XFramework.Wallets.Api.Entities;

/// <summary>
/// VSA wrapper entity for WalletTransfer domain model.
/// Implements the VSA Wrapper Entity Pattern (ADR-003).
/// Note: This entity is READ-ONLY. Use IWalletService for transfer operations.
/// </summary>
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.Get | EndpointActions.GetList,
    RoutePrefix = "api/wallet-transfers",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "wallet-transfers"
)]
public partial class WalletTransferEntity
{
    public Guid Id { get; set; }
    public TransactionPurpose TransactionPurpose { get; set; }
    public Guid SenderTransactionId { get; set; }
    public Guid RecipientTransactionId { get; set; }
    
    // Stored computed value (from domain's Amount property)
    public decimal Amount { get; set; }
    
    public decimal TransactionFee { get; set; }
    
    // Stored computed value (from domain's TotalFees property)
    public decimal TotalFees { get; set; }
    
    // ISoftDeletable properties
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Request DTO for listing WalletTransferEntities with pagination.
/// Note: Create and Update operations should use IWalletService.
/// </summary>
public class GetWalletTransferEntityListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public Guid? SenderTransactionId { get; set; }
    public Guid? RecipientTransactionId { get; set; }
    public TransactionPurpose? TransactionPurpose { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}