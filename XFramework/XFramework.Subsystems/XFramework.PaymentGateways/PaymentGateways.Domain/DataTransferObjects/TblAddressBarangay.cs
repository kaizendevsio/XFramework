using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects
{
    public partial class TblAddressBarangay
    {
        public TblAddressBarangay()
        {
            TblIdentityAddresses = new HashSet<TblIdentityAddress>();
        }

        public long Id { get; set; }
        public long? Code { get; set; }
        public string Description { get; set; }
        public long? CityCode { get; set; }

        public virtual TblAddressCity CityCodeNavigation { get; set; }
        public virtual ICollection<TblIdentityAddress> TblIdentityAddresses { get; set; }
    }
}
