using System;

#nullable disable

namespace Coins.Domain.DataTransferObjects
{
    public partial class TblIdentityAddress
    {
        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public long? IdentityInfoId { get; set; }
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
        public long? Country { get; set; }

        public virtual TblIdentityAddressEntity AddressEntities { get; set; }
        public virtual TblAddressBarangay BarangayNavigation { get; set; }
        public virtual TblAddressCity CityNavigation { get; set; }
        public virtual TblAddressCountry CountryNavigation { get; set; }
        public virtual TblIdentityInformation IdentityInfo { get; set; }
        public virtual TblAddressProvince ProvinceNavigation { get; set; }
        public virtual TblAddressRegion RegionNavigation { get; set; }
    }
}
