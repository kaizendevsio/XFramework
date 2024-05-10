namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class AddressProvince : BaseModel
{
    
    [MemoryPackOrder(0)]
    public long PsgcCode { get; set; }

    [MemoryPackOrder(1)]
    public string? Description { get; set; }

    [MemoryPackOrder(2)]
    public long RegCodeId { get; set; }

    [MemoryPackOrder(3)]
    public long Code { get; set; }


    [MemoryPackOrder(4)]
    public virtual ICollection<AddressCity> AddressCities { get; set; } = new List<AddressCity>();

    [MemoryPackOrder(5)]
    public virtual ICollection<IdentityAddress> IdentityAddresses { get; set; } = new List<IdentityAddress>();

    [MemoryPackOrder(6)]
    public virtual AddressRegion RegCode { get; set; } = null!;
}
