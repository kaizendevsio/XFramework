namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class IdentityAddress : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid IdentityInfoId { get; set; }

    [MemoryPackOrder(1)]
    public string? UnitNumber { get; set; }

    [MemoryPackOrder(2)]
    public string? Street { get; set; }

    [MemoryPackOrder(3)]
    public string? Building { get; set; }
    [MemoryPackOrder(4)]
    public string? Name { get; set; }

    [MemoryPackOrder(5)]
    public Guid BarangayId { get; set; }

    [MemoryPackOrder(6)]
    public Guid CityId { get; set; }

    [MemoryPackOrder(7)]
    public string? Subdivision { get; set; }

    [MemoryPackOrder(8)]
    public Guid RegionId { get; set; }

    [MemoryPackOrder(9)]
    public Guid? AddressTypeId { get; set; }

    [MemoryPackOrder(10)]
    public bool? DefaultAddress { get; set; }

    [MemoryPackOrder(11)]
    public Guid ProvinceId { get; set; }

    [MemoryPackOrder(12)]
    public Guid CountryId { get; set; }


    [MemoryPackOrder(13)]
    public virtual IdentityAddressType? AddressType { get; set; }

    [MemoryPackOrder(14)]
    public virtual AddressBarangay? Barangay { get; set; }

    [MemoryPackOrder(15)]
    public virtual AddressCity? City { get; set; }

    [MemoryPackOrder(16)]
    public virtual AddressCountry? Country { get; set; }

    [MemoryPackOrder(17)]
    public virtual IdentityInformation? IdentityInfo { get; set; }

    [MemoryPackOrder(18)]
    public virtual AddressProvince? Province { get; set; }

    [MemoryPackOrder(19)]
    public virtual AddressRegion? Region { get; set; }
}
