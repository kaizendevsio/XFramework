using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects.Legacy;

public partial class TblAddressProvince
{
    public TblAddressProvince()
    {
        TblAddressCity = new HashSet<TblAddressCity>();
        TblUserAddresses = new HashSet<TblUserAddresses>();
    }

    public long Id { get; set; }
    public long? PsgcCode { get; set; }
    public string Description { get; set; }
    public long? RegCode { get; set; }
    public long? Code { get; set; }

    public virtual TblAddressRegions RegCodeNavigation { get; set; }
    public virtual ICollection<TblAddressCity> TblAddressCity { get; set; }
    public virtual ICollection<TblUserAddresses> TblUserAddresses { get; set; }
}