using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects
{
    public partial class TblAddressRegion
    {
        public TblAddressRegion()
        {
            TblAddressProvinces = new HashSet<TblAddressProvince>();
            TblIdentityAddresses = new HashSet<TblIdentityAddress>();
        }

        public long Id { get; set; }
        public int PsgcCode { get; set; }
        public string Description { get; set; }
        public long Code { get; set; }

        public virtual ICollection<TblAddressProvince> TblAddressProvinces { get; set; }
        public virtual ICollection<TblIdentityAddress> TblIdentityAddresses { get; set; }
    }
}
