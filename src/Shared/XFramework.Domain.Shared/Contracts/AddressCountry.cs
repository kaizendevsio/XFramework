namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class AddressCountry : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string? IsoCode2 { get; set; }

    [MemoryPackOrder(1)]
    public string? IsoCode3 { get; set; }

    [MemoryPackOrder(2)]
    public string? Name { get; set; }

    [MemoryPackOrder(3)]
    public string? Language { get; set; }

    [MemoryPackOrder(4)]
    public string? PhoneCountryCode { get; set; }

    [MemoryPackOrder(5)]
    public Guid CurrencyId { get; set; }


    [MemoryPackOrder(6)]
    public virtual ICollection<AddressRegion> AddressRegions { get; set; } = new List<AddressRegion>();

    [MemoryPackOrder(7)]
    public virtual CurrencyType? Currency { get; set; }

    [MemoryPackOrder(8)]
    public virtual ICollection<IdentityAddress> IdentityAddresses { get; set; } = new List<IdentityAddress>();
}
