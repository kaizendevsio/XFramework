using System;
using System.Collections.Generic;

namespace Wallets.Domain.DataTransferObjects;

public partial class CurrencyEntity
{
    public long Id { get; set; }

    public bool? IsEnabled { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public long? ModifiedBy { get; set; }

    public string? Name { get; set; }

    public string? CurrencyIsoCode3 { get; set; }

    public string? Description { get; set; }

    public short? CurrencyType { get; set; }

    public string? Guid { get; set; }

    public virtual ICollection<AddressCountry> AddressCountries { get; } = new List<AddressCountry>();

    public virtual ICollection<DepositRequest> DepositRequests { get; } = new List<DepositRequest>();

    public virtual ICollection<ExchangeRate> ExchangeRateSourceCurrencyEntities { get; } = new List<ExchangeRate>();

    public virtual ICollection<ExchangeRate> ExchangeRateTargetCurrencyEntities { get; } = new List<ExchangeRate>();

    public virtual ICollection<WalletEntity> WalletEntities { get; } = new List<WalletEntity>();
}
