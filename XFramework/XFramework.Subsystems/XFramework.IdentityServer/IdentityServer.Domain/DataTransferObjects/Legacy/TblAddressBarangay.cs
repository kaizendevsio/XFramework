using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects.Legacy;

public partial class TblAddressBarangay
{
    public TblAddressBarangay()
    {
        TblUserAddresses = new HashSet<TblUserAddresses>();
    }

    public long Id { get; set; }
    public long? Code { get; set; }
    public string Description { get; set; }
    public long? CityCode { get; set; }

    public virtual TblAddressCity CityCodeNavigation { get; set; }
    public virtual ICollection<TblUserAddresses> TblUserAddresses { get; set; }
}