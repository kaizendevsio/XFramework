using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects
{
    public partial class TblAddressCountry
    {
        public TblAddressCountry()
        {
            TblAddressRegions = new HashSet<TblAddressRegion>();
            TblIdentityAddresses = new HashSet<TblIdentityAddress>();
        }

        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime? CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? LastChanged { get; set; }
        public string IsoCode2 { get; set; }
        public string IsoCode3 { get; set; }
        public string Name { get; set; }
        public string Language { get; set; }
        public string PhoneCountryCode { get; set; }
        public long? CurrencyId { get; set; }
        public string Guid { get; set; }

        public virtual TblCurrencyEntity Currency { get; set; }
        public virtual ICollection<TblAddressRegion> TblAddressRegions { get; set; }
        public virtual ICollection<TblIdentityAddress> TblIdentityAddresses { get; set; }
    }
}
