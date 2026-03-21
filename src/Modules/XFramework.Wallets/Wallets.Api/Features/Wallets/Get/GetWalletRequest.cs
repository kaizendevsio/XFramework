namespace Wallets.Api.Features.Wallets.Get;

/// <summary>
/// Request DTO for getting a wallet by ID
/// </summary>
public record GetWalletRequest
{
    /// <summary>
    /// The wallet ID to retrieve
    /// </summary>
    public required Guid WalletId { get; set; }

    /// <summary>
    /// The tenant ID for multi-tenancy support
    /// </summary>
    public required Guid TenantId { get; set; }
}