using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects.Legacy
{
    public partial class TblAddressCity
    {
        public TblAddressCity()
        {
            TblAddressBarangay = new HashSet<TblAddressBarangay>();
            TblUserAddresses = new HashSet<TblUserAddresses>();
        }

        public long Id { get; set; }
        public long? PsgcCode { get; set; }
        public string Description { get; set; }
        public long? ProvCode { get; set; }
        public long? Code { get; set; }

        public virtual TblAddressProvince ProvCodeNavigation { get; set; }
        public virtual ICollection<TblAddressBarangay> TblAddressBarangay { get; set; }
        public virtual ICollection<TblUserAddresses> TblUserAddresses { get; set; }
    }
}
