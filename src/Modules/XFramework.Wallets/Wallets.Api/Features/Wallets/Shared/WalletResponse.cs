namespace Wallets.Api.Features.Wallets.Shared;

/// <summary>
/// Response DTO for Wallet operations
/// </summary>
public class WalletResponse
{
    public Guid Id { get; set; }
    public Guid CredentialId { get; set; }
    public Guid? WalletTypeId { get; set; }
    public string? WalletTypeName { get; set; }
    public decimal Balance { get; set; }
    public decimal DebitOnHoldBalance { get; set; }
    public decimal CreditOnHoldBalance { get; set; }
    public decimal TransferableBalance { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal? TotalBalance { get; set; }
    public string? AccountNumber { get; set; }
    public int CardNumber { get; set; }
    public decimal? MinTransferRule { get; set; }
    public decimal? MaxTransferRule { get; set; }
    public decimal? BondBalanceRule { get; set; }
    public decimal? MaintainingBalanceRule { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    /// Maps a Wallet entity to WalletResponse
    /// </summary>
    public static WalletResponse FromWallet(Wallet wallet)
    {
        return new WalletResponse
        {
            Id = wallet.Id,
            CredentialId = wallet.CredentialId,
            WalletTypeId = wallet.WalletTypeId,
            WalletTypeName = wallet.WalletType?.Name,
            Balance = wallet.Balance,
            DebitOnHoldBalance = wallet.DebitOnHoldBalance,
            CreditOnHoldBalance = wallet.CreditOnHoldBalance,
            TransferableBalance = wallet.TransferableBalance,
            AvailableBalance = wallet.AvailableBalance,
            TotalBalance = wallet.TotalBalance,
            AccountNumber = wallet.AccountNumber,
            CardNumber = wallet.CardNumber,
            MinTransferRule = wallet.MinTransferRule,
            MaxTransferRule = wallet.MaxTransferRule,
            BondBalanceRule = wallet.BondBalanceRule,
            MaintainingBalanceRule = wallet.MaintainingBalanceRule,
            CreatedAt = wallet.CreatedAt,
            ModifiedAt = wallet.ModifiedAt
        };
    }
}

/// <summary>
/// Paginated response for wallets
/// </summary>
public class PaginatedWalletResponse
{
    public List<WalletResponse> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasPrevious { get; set; }
    public bool HasNext { get; set; }
}