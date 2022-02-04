using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects.Legacy;

public partial class TblAddressRegions
{
    public TblAddressRegions()
    {
        TblAddressProvince = new HashSet<TblAddressProvince>();
        TblUserAddresses = new HashSet<TblUserAddresses>();
    }

    public long Id { get; set; }
    public int? PsgcCode { get; set; }
    public string Description { get; set; }
    public long? Code { get; set; }

    public virtual ICollection<TblAddressProvince> TblAddressProvince { get; set; }
    public virtual ICollection<TblUserAddresses> TblUserAddresses { get; set; }
}