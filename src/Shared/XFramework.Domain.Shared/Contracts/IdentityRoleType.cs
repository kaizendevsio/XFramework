namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class IdentityRoleType : BaseModel, IHasSystemReferenceId
{
    
    [MemoryPackOrder(0)]
    public string? Name { get; set; }

    [MemoryPackOrder(1)]
    public short? RoleLevel { get; set; }

    [MemoryPackOrder(2)]
    public Guid GroupId { get; set; }

    [MemoryPackOrder(3)]
    public virtual Tenant Tenant { get; set; } = null!;

    [MemoryPackOrder(4)]
    public virtual IdentityRoleTypeGroup? Group { get; set; }

    [MemoryPackOrder(5)]
    public virtual ICollection<IdentityRole> IdentityRoles { get; set; } = new List<IdentityRole>();

    [MemoryPackOrder(200)]
    public Guid SystemReferenceId { get; set; }
}
