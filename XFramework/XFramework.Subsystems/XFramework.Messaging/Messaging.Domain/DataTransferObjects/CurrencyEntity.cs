using System;
using System.Collections.Generic;

namespace Messaging.Domain.DataTransferObjects
{
    public partial class CurrencyEntity
    {
        public CurrencyEntity()
        {
            AddressCountries = new HashSet<AddressCountry>();
            ExchangeRateSourceCurrencyEntities = new HashSet<ExchangeRate>();
            ExchangeRateTargetCurrencyEntities = new HashSet<ExchangeRate>();
        }

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

        public virtual ICollection<AddressCountry> AddressCountries { get; set; }
        public virtual ICollection<ExchangeRate> ExchangeRateSourceCurrencyEntities { get; set; }
        public virtual ICollection<ExchangeRate> ExchangeRateTargetCurrencyEntities { get; set; }
    }
}
