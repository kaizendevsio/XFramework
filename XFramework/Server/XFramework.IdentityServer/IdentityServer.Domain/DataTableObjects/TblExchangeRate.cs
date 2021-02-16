using System;
using System.Collections.Generic;

#nullable disable

namespace IdentityServer.Domain.DataTableObjects
{
    public partial class TblExchangeRate
    {
        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public long SourceCurrencyEntityId { get; set; }
        public long TargetCurrencyEntityId { get; set; }
        public decimal? Value { get; set; }
        public decimal? Fee { get; set; }
        public DateTime? EffectivityDate { get; set; }
        public DateTime? ExpiryDate { get; set; }

        public virtual TblCurrencyEntity SourceCurrencyEntity { get; set; }
        public virtual TblCurrencyEntity TargetCurrencyEntity { get; set; }
    }
}
