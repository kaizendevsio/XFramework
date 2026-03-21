using XFramework.Core.Attributes;
using XFramework.Domain.Shared.Enums;

namespace XFramework.Wallets.Api.Entities;

/// <summary>
/// VSA wrapper entity for WalletTransaction domain model.
/// Implements the VSA Wrapper Entity Pattern (ADR-003).
/// Note: This entity is READ-ONLY. Use IWalletService for transaction operations.
/// </summary>
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.Get | EndpointActions.GetList,
    RoutePrefix = "api/wallet-transactions",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "wallet-transactions"
)]
public partial class WalletTransactionEntity
{
    public Guid Id { get; set; }
    public Guid CredentialId { get; set; }
    public Guid WalletId { get; set; }
    
    // Stored computed value (from domain's Amount property)
    public decimal Amount { get; set; }
    
    // Stored computed value (from domain's NetAmount property)
    public decimal NetAmount { get; set; }
    
    public bool Held { get; set; }
    public bool Released { get; set; }
    public string? Remarks { get; set; }
    public decimal TransactionFee { get; set; }
    
    // Stored computed value (from domain's TotalFees property)
    public decimal TotalFees { get; set; }
    
    public string? ReferenceNumber { get; set; }
    public string? Description { get; set; }
    public decimal? RunningTotalBalance { get; set; }
    public decimal? RunningAvailableBalance { get; set; }
    public decimal? RunningBalance { get; set; }
    public decimal? RunningDebitOnHoldBalance { get; set; }
    public decimal? RunningCreditOnHoldBalance { get; set; }
    public decimal PreviousTotalBalance { get; set; }
    public decimal PreviousBalance { get; set; }
    public decimal PreviousDebitOnHoldBalance { get; set; }
    public decimal PreviousCreditOnHoldBalance { get; set; }
    public TransactionType? TransactionType { get; set; }
    
    // ISoftDeletable properties
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Request DTO for listing WalletTransactionEntities with pagination.
/// Note: Create and Update operations should use IWalletService.
/// </summary>
public class GetWalletTransactionEntityListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? CredentialId { get; set; }
    public Guid? WalletId { get; set; }
    public TransactionType? TransactionType { get; set; }
    public bool? Held { get; set; }
    public bool? Released { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}