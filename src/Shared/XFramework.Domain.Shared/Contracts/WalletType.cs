using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class WalletType : BaseModel
{
    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Desc { get; set; }

    public short Type { get; set; }

    public Guid? CurrencyTypeId { get; set; }

    public decimal? MinTransferRule { get; set; }

    public decimal? MaxTransferRule { get; set; }
    
    public decimal? BondBalanceRule { get; set; }
    
    public decimal? MaintainingBalanceRule { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;

    public virtual CurrencyType? CurrencyType { get; set; }

    public virtual ICollection<DepositRequest> DepositRequests { get; set; } = new List<DepositRequest>();
    public virtual ICollection<WithdrawalRequest> WithdrawalRequests { get; set; } = new List<WithdrawalRequest>();

    public virtual ICollection<Wallet> Wallets { get; set; } = new List<Wallet>();
}