using XFramework.Core.Attributes;

namespace XFramework.Wallets.Api.Entities;

/// <summary>
/// VSA wrapper entity for WalletAddress domain model.
/// Implements the VSA Wrapper Entity Pattern (ADR-003).
/// </summary>
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/wallet-addresses",
    RequireAuthorization = true,
    CacheDurationSeconds = 600,
    CacheKeyPrefix = "wallet-addresses"
)]
public partial class WalletAddressEntity
{
    public Guid Id { get; set; }
    public string? Address { get; set; }
    public decimal? Balance { get; set; }
    public string? Remarks { get; set; }
    public Guid WalletId { get; set; }
    
    // ISoftDeletable properties
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Request DTO for creating a WalletAddressEntity.
/// </summary>
public class CreateWalletAddressEntityRequest
{
    public string? Address { get; set; }
    public decimal? Balance { get; set; }
    public string? Remarks { get; set; }
    public Guid WalletId { get; set; }
}

/// <summary>
/// Request DTO for updating a WalletAddressEntity.
/// </summary>
public class UpdateWalletAddressEntityRequest
{
    public string? Address { get; set; }
    public decimal? Balance { get; set; }
    public string? Remarks { get; set; }
}

/// <summary>
/// Request DTO for listing WalletAddressEntities with pagination.
/// </summary>
public class GetWalletAddressEntityListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? WalletId { get; set; }
}