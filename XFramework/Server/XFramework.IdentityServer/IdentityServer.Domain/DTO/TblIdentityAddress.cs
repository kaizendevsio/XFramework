using System;
using System.Collections.Generic;

#nullable disable

namespace IdentityServer.Domain.DTO
{
    public partial class TblIdentityAddress
    {
        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
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
        public virtual TblIdentityInfo IdentityInfo { get; set; }
        public virtual TblAddressProvince ProvinceNavigation { get; set; }
        public virtual TblAddressRegion RegionNavigation { get; set; }
    }
}
