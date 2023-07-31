using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects;

public partial class CurrencyEntity
{
    public CurrencyEntity()
    {
        AddressCountries = new HashSet<AddressCountry>();
        DepositRequests = new HashSet<DepositRequest>();
        ExchangeRateSourceCurrencyEntities = new HashSet<ExchangeRate>();
        ExchangeRateTargetCurrencyEntities = new HashSet<ExchangeRate>();
        WalletEntities = new HashSet<WalletEntity>();
    }

    public long Id { get; set; }
    public bool? IsEnabled { get; set; }
    public DateTime? CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public long? ModifiedBy { get; set; }
    public string Name { get; set; }
    public string CurrencyIsoCode3 { get; set; }
    public string Description { get; set; }
    public short? CurrencyType { get; set; }
    public string Guid { get; set; }

    public virtual ICollection<AddressCountry> AddressCountries { get; set; }
    public virtual ICollection<DepositRequest> DepositRequests { get; set; }
    public virtual ICollection<ExchangeRate> ExchangeRateSourceCurrencyEntities { get; set; }
    public virtual ICollection<ExchangeRate> ExchangeRateTargetCurrencyEntities { get; set; }
    public virtual ICollection<WalletEntity> WalletEntities { get; set; }
}