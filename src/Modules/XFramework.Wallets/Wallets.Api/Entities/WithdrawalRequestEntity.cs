using XFramework.Core.Attributes;
using XFramework.Domain.Shared.Enums;

namespace XFramework.Wallets.Api.Entities;

/// <summary>
/// VSA wrapper entity for WithdrawalRequest domain model.
/// Implements the VSA Wrapper Entity Pattern (ADR-003).
/// </summary>
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/withdrawal-requests",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "withdrawal-requests"
)]
public partial class WithdrawalRequestEntity
{
    public Guid Id { get; set; }
    public Guid CredentialId { get; set; }
    public string? Address { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Fee { get; set; }
    public TransactionStatus WithdrawalStatus { get; set; }
    public string? Remarks { get; set; }
    public string? ReferenceNumber { get; set; }
    public Guid WalletId { get; set; }
    
    // ISoftDeletable properties
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Request DTO for creating a WithdrawalRequestEntity.
/// </summary>
public class CreateWithdrawalRequestEntityRequest
{
    public Guid CredentialId { get; set; }
    public string? Address { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Fee { get; set; }
    public TransactionStatus WithdrawalStatus { get; set; }
    public string? Remarks { get; set; }
    public string? ReferenceNumber { get; set; }
    public Guid WalletId { get; set; }
}

/// <summary>
/// Request DTO for updating a WithdrawalRequestEntity.
/// </summary>
public class UpdateWithdrawalRequestEntityRequest
{
    public string? Address { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Fee { get; set; }
    public TransactionStatus WithdrawalStatus { get; set; }
    public string? Remarks { get; set; }
    public string? ReferenceNumber { get; set; }
}

/// <summary>
/// Request DTO for listing WithdrawalRequestEntities with pagination.
/// </summary>
public class GetWithdrawalRequestEntityListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? CredentialId { get; set; }
    public Guid? WalletId { get; set; }
    public TransactionStatus? WithdrawalStatus { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}