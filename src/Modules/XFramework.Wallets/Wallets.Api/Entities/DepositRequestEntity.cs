using XFramework.Core.Attributes;

namespace XFramework.Wallets.Api.Entities;

/// <summary>
/// VSA wrapper entity for DepositRequest domain model.
/// Implements the VSA Wrapper Entity Pattern (ADR-003).
/// </summary>
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/deposit-requests",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "deposit-requests"
)]
public partial class DepositRequestEntity
{
    public Guid Id { get; set; }
    public Guid CredentialId { get; set; }
    public Guid? SourceCurrencyId { get; set; }
    public Guid? WalletTypeId { get; set; }
    public string? Address { get; set; }
    public decimal? Amount { get; set; }
    public string? Remarks { get; set; }
    public short? DepositStatus { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? RawRequestData { get; set; }
    public string? ReferenceNo { get; set; }
    public string? RawResponseData { get; set; }
    public decimal? Discount { get; set; }
    public decimal? ConvenienceFee { get; set; }
    public decimal? SystemFee { get; set; }
    public int? DiscountType { get; set; }
    public Guid GatewayId { get; set; }
    
    // ISoftDeletable properties
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Request DTO for creating a DepositRequestEntity.
/// </summary>
public class CreateDepositRequestEntityRequest
{
    public Guid CredentialId { get; set; }
    public Guid? SourceCurrencyId { get; set; }
    public Guid? WalletTypeId { get; set; }
    public string? Address { get; set; }
    public decimal? Amount { get; set; }
    public string? Remarks { get; set; }
    public short? DepositStatus { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? RawRequestData { get; set; }
    public string? ReferenceNo { get; set; }
    public decimal? Discount { get; set; }
    public decimal? ConvenienceFee { get; set; }
    public decimal? SystemFee { get; set; }
    public int? DiscountType { get; set; }
    public Guid GatewayId { get; set; }
}

/// <summary>
/// Request DTO for updating a DepositRequestEntity.
/// </summary>
public class UpdateDepositRequestEntityRequest
{
    public string? Address { get; set; }
    public decimal? Amount { get; set; }
    public string? Remarks { get; set; }
    public short? DepositStatus { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? RawResponseData { get; set; }
    public decimal? Discount { get; set; }
    public decimal? ConvenienceFee { get; set; }
    public decimal? SystemFee { get; set; }
}

/// <summary>
/// Request DTO for listing DepositRequestEntities with pagination.
/// </summary>
public class GetDepositRequestEntityListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? CredentialId { get; set; }
    public Guid? WalletTypeId { get; set; }
    public Guid? SourceCurrencyId { get; set; }
    public short? DepositStatus { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}