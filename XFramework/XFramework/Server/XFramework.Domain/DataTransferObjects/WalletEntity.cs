using System;
using System.Collections.Generic;

namespace XFramework.Domain.DataTransferObjects;

public partial class WalletEntity
{
    public long Id { get; set; }

    public bool? IsEnabled { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public long? ModifiedBy { get; set; }

    public DateTime? LastChanged { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Desc { get; set; }

    public short Type { get; set; }

    public long? CurrencyEntityId { get; set; }

    public decimal? MinTransfer { get; set; }

    public decimal? MaxTransfer { get; set; }

    public string? Guid { get; set; }

    public bool IsDeleted { get; set; }

    public long ApplicationId { get; set; }

    public virtual Application Application { get; set; } = null!;

    public virtual CurrencyEntity? CurrencyEntity { get; set; }

    public virtual ICollection<DepositRequest> DepositRequests { get; } = new List<DepositRequest>();

    public virtual ICollection<Wallet> Wallets { get; } = new List<Wallet>();

    public virtual ICollection<WithdrawalRequest> WithdrawalRequestSourceWalletTypes { get; } = new List<WithdrawalRequest>();

    public virtual ICollection<WithdrawalRequest> WithdrawalRequestTargetCurrencies { get; } = new List<WithdrawalRequest>();
}
