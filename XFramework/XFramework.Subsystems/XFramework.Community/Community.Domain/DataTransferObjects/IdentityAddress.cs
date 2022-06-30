using System;
using System.Collections.Generic;

namespace Community.Domain.DataTransferObjects
{
    public partial class IdentityAddress
    {
        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public long? IdentityInfoId { get; set; }
        public string? UnitNumber { get; set; }
        public string? Street { get; set; }
        public string? Building { get; set; }
        public long? Barangay { get; set; }
        public long? City { get; set; }
        public string? Subdivision { get; set; }
        public long? Region { get; set; }
        public long? AddressEntitiesId { get; set; }
        public bool? DefaultAddress { get; set; }
        public long? Province { get; set; }
        public long? Country { get; set; }
        public string Guid { get; set; } = null!;

        public virtual IdentityAddressEntity? AddressEntities { get; set; }
        public virtual AddressBarangay? BarangayNavigation { get; set; }
        public virtual AddressCity? CityNavigation { get; set; }
        public virtual AddressCountry? CountryNavigation { get; set; }
        public virtual IdentityInformation? IdentityInfo { get; set; }
        public virtual AddressProvince? ProvinceNavigation { get; set; }
        public virtual AddressRegion? RegionNavigation { get; set; }
    }
}
