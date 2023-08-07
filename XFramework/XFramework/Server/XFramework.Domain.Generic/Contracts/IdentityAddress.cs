namespace XFramework.Domain.Generic.Contracts;

public partial class IdentityAddress
{
    public Guid Id { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public long? ModifiedBy { get; set; }

    public bool IsDeleted { get; set; }

    public Guid IdentityInfoId { get; set; }

    public string? UnitNumber { get; set; }

    public string? Street { get; set; }

    public string? Building { get; set; }

    public Guid BarangayId { get; set; }

    public Guid CityId { get; set; }

    public string? Subdivision { get; set; }

    public Guid RegionId { get; set; }

    public Guid? AddressTypeId { get; set; }

    public bool? DefaultAddress { get; set; }

    public Guid ProvinceId { get; set; }

    public Guid CountryId { get; set; }

    
    public virtual IdentityAddressType? AddressType { get; set; }

    public virtual AddressBarangay? Barangay { get; set; }

    public virtual AddressCity? City { get; set; }

    public virtual AddressCountry? Country { get; set; }

    public virtual IdentityInformation? IdentityInfo { get; set; }

    public virtual AddressProvince? Province { get; set; }

    public virtual AddressRegion? Region { get; set; }
}
