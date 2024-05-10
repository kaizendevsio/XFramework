namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class AddressRegion : BaseModel
{
    
    [MemoryPackOrder(0)]
    public int PsgcCode { get; set; }

    [MemoryPackOrder(1)]
    public string? Description { get; set; }

    [MemoryPackOrder(2)]
    public long Code { get; set; }


    [MemoryPackOrder(3)]
    public Guid CountryId { get; set; }

    [MemoryPackOrder(4)]
    public virtual ICollection<AddressProvince> AddressProvinces { get; set; } = new List<AddressProvince>();

    [MemoryPackOrder(5)]
    public virtual AddressCountry? Country { get; set; }

    [MemoryPackOrder(6)]
    public virtual ICollection<IdentityAddress> IdentityAddresses { get; set; } = new List<IdentityAddress>();
}
