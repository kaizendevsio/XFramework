using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects;

public partial class WalletEntity
{
    public WalletEntity()
    {
        DepositRequests = new HashSet<DepositRequest>();
        Wallets = new HashSet<Wallet>();
        WithdrawalRequestSourceWalletTypes = new HashSet<WithdrawalRequest>();
        WithdrawalRequestTargetCurrencies = new HashSet<WithdrawalRequest>();
    }

    public long Id { get; set; }
    public bool? IsEnabled { get; set; }
    public DateTime? CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public long? ModifiedBy { get; set; }
    public DateTime? LastChanged { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string Desc { get; set; }
    public short Type { get; set; }
    public long? CurrencyEntityId { get; set; }
    public decimal? MinTransfer { get; set; }
    public decimal? MaxTransfer { get; set; }
    public string Guid { get; set; }
    public bool IsDeleted { get; set; }
    public long ApplicationId { get; set; }

    public virtual Application Application { get; set; }
    public virtual CurrencyEntity CurrencyEntity { get; set; }
    public virtual ICollection<DepositRequest> DepositRequests { get; set; }
    public virtual ICollection<Wallet> Wallets { get; set; }
    public virtual ICollection<WithdrawalRequest> WithdrawalRequestSourceWalletTypes { get; set; }
    public virtual ICollection<WithdrawalRequest> WithdrawalRequestTargetCurrencies { get; set; }
}