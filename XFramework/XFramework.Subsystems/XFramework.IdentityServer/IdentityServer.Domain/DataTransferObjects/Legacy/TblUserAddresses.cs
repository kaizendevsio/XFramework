using System;

namespace IdentityServer.Domain.DataTransferObjects.Legacy;

public partial class TblUserAddresses
{
    public long Id { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedOn { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public long? ModifiedBy { get; set; }
    public bool IsDeleted { get; set; }
    public long? UserInfoId { get; set; }
    public string UnitNumber { get; set; }
    public string Street { get; set; }
    public string Building { get; set; }
    public long? Barangay { get; set; }
    public long? City { get; set; }
    public string Subdivision { get; set; }
    public long? Region { get; set; }
    public long? AddressEntitiesId { get; set; }
    public bool? DefaultAddress { get; set; }
    public long? Province { get; set; }

    public virtual TblAddressEntities AddressEntities { get; set; }
    public virtual TblAddressBarangay BarangayNavigation { get; set; }
    public virtual TblAddressCity CityNavigation { get; set; }
    public virtual TblAddressProvince ProvinceNavigation { get; set; }
    public virtual TblAddressRegions RegionNavigation { get; set; }
    public virtual TblUserInfo UserInfo { get; set; }
}