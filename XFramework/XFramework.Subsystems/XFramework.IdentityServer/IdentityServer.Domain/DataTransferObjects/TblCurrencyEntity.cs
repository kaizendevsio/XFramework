using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects
{
    public partial class TblCurrencyEntity
    {
        public TblCurrencyEntity()
        {
            TblAddressCountries = new HashSet<TblAddressCountry>();
            TblExchangeRateSourceCurrencyEntities = new HashSet<TblExchangeRate>();
            TblExchangeRateTargetCurrencyEntities = new HashSet<TblExchangeRate>();
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

        public virtual ICollection<TblAddressCountry> TblAddressCountries { get; set; }
        public virtual ICollection<TblExchangeRate> TblExchangeRateSourceCurrencyEntities { get; set; }
        public virtual ICollection<TblExchangeRate> TblExchangeRateTargetCurrencyEntities { get; set; }
    }
}
