using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects
{
    public partial class TblAddressCity
    {
        public TblAddressCity()
        {
            TblAddressBarangays = new HashSet<TblAddressBarangay>();
            TblIdentityAddresses = new HashSet<TblIdentityAddress>();
        }

        public long Id { get; set; }
        public long PsgcCode { get; set; }
        public string Description { get; set; }
        public long ProvCode { get; set; }
        public long Code { get; set; }

        public virtual TblAddressProvince ProvCodeNavigation { get; set; }
        public virtual ICollection<TblAddressBarangay> TblAddressBarangays { get; set; }
        public virtual ICollection<TblIdentityAddress> TblIdentityAddresses { get; set; }
    }
}
