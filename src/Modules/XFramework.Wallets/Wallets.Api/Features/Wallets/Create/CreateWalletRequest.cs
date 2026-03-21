namespace Wallets.Api.Features.Wallets.Create;

/// <summary>
/// Request DTO for creating a new wallet
/// </summary>
public record CreateWalletRequest
{
    /// <summary>
    /// The credential ID that will own the wallet
    /// </summary>
    public required Guid CredentialId { get; set; }

    /// <summary>
    /// The type of wallet to create
    /// </summary>
    public required Guid WalletTypeId { get; set; }

    /// <summary>
    /// Optional initial balance (default: 0)
    /// </summary>
    public decimal InitialBalance { get; set; } = 0;

    /// <summary>
    /// The tenant ID for multi-tenancy support
    /// </summary>
    public required Guid TenantId { get; set; }
}