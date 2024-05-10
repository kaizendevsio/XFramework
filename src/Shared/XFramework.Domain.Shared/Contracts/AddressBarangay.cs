namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class AddressBarangay : BaseModel
{
    
    [MemoryPackOrder(0)]
    public long? Code { get; set; }

    [MemoryPackOrder(1)]
    public string? Description { get; set; }

    [MemoryPackOrder(2)]
    public long CityCodeId { get; set; }

    [MemoryPackOrder(3)]
    public int? RegCode { get; set; }

    [MemoryPackOrder(4)]
    public int? ProvCode { get; set; }


    [MemoryPackOrder(5)]
    public virtual AddressCity? CityCode { get; set; }

    [MemoryPackOrder(6)]
    public virtual ICollection<IdentityAddress> IdentityAddresses { get; set; } = new List<IdentityAddress>();
}
