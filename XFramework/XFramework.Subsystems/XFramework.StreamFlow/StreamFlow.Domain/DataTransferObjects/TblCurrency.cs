using System;
using System.Collections.Generic;

#nullable disable

namespace StreamFlow.Domain.DataTransferObjects;

public partial class TblCurrency
{
    public TblCurrency()
    {
        TblAddressCountries = new HashSet<TblAddressCountry>();
        TblExchangeRateSourceCurrencies = new HashSet<TblExchangeRate>();
        TblExchangeRateTargetCurrencies = new HashSet<TblExchangeRate>();
    }

    public long Id { get; set; }
    public bool? IsEnabled { get; set; }
    public DateTime? CreatedOn { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public long? ModifiedBy { get; set; }
    public DateTime? LastChanged { get; set; }
    public string Name { get; set; }
    public string CurrencyIsoCode3 { get; set; }
    public string Description { get; set; }
    public short? CurrencyType { get; set; }

    public virtual ICollection<TblAddressCountry> TblAddressCountries { get; set; }
    public virtual ICollection<TblExchangeRate> TblExchangeRateSourceCurrencies { get; set; }
    public virtual ICollection<TblExchangeRate> TblExchangeRateTargetCurrencies { get; set; }
}