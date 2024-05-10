namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class IdentityAddressType : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string? Name { get; set; }


    [MemoryPackOrder(1)]
    public virtual ICollection<IdentityAddress> IdentityAddresses { get; set; } = new List<IdentityAddress>();
}
