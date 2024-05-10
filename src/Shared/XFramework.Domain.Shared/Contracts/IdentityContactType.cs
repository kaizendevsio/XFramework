namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class IdentityContactType : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string? Name { get; set; }


    [MemoryPackOrder(1)]
    public virtual ICollection<IdentityContact> IdentityContacts { get; set; } = new List<IdentityContact>();
}
