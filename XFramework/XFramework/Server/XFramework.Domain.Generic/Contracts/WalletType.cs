﻿namespace XFramework.Domain.Generic.Contracts;

public partial class WalletType : BaseModel
{
    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Desc { get; set; }

    public short Type { get; set; }

    public Guid? CurrencyTypeId { get; set; }

    public decimal? MinTransfer { get; set; }

    public decimal? MaxTransfer { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;

    public virtual CurrencyType? CurrencyType { get; set; }

    public virtual ICollection<DepositRequest> DepositRequests { get; set; } = new List<DepositRequest>();
    public virtual ICollection<WithdrawalRequest> WithdrawalRequests { get; set; } = new List<WithdrawalRequest>();

    public virtual ICollection<Wallet> Wallets { get; set; } = new List<Wallet>();
}