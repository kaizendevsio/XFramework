using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class WalletAddress : BaseModel
{
    public string? Address { get; set; }

    public decimal? Balance { get; set; }

    public string? Remarks { get; set; }


    public Guid WalletId { get; set; }

    public virtual Wallet Wallet { get; set; } = null!;
}