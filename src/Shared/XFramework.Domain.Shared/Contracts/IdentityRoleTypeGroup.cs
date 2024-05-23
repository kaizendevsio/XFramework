namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class IdentityRoleTypeGroup : BaseModel, IHasSystemReferenceId
{
    
    [MemoryPackOrder(0)]
    public string Name { get; set; } = null!;

    [MemoryPackOrder(1)]
    public string Description { get; set; } = null!;


    [MemoryPackOrder(2)]
    public virtual ICollection<IdentityRoleType> IdentityRoleTypes { get; set; } = new List<IdentityRoleType>();

    [MemoryPackOrder(200)]
    public Guid SystemReferenceId { get; set; }
}
