using System.Collections.Generic;

#nullable disable

namespace Wallets.Domain.DataTransferObjects
{
    public partial class TblAddressProvince
    {
        public TblAddressProvince()
        {
            TblAddressCities = new HashSet<TblAddressCity>();
            TblIdentityAddresses = new HashSet<TblIdentityAddress>();
        }

        public long Id { get; set; }
        public long PsgcCode { get; set; }
        public string Description { get; set; }
        public long RegCode { get; set; }
        public long Code { get; set; }

        public virtual TblAddressRegion RegCodeNavigation { get; set; }
        public virtual ICollection<TblAddressCity> TblAddressCities { get; set; }
        public virtual ICollection<TblIdentityAddress> TblIdentityAddresses { get; set; }
    }
}
