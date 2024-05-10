namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class MessageThreadMemberRole : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid MessageThreadMemberId { get; set; }

    [MemoryPackOrder(1)]
    public Guid RoleId { get; set; }

    [MemoryPackOrder(2)]
    public virtual MessageThreadMember MessageThreadMember { get; set; } = null!;

    [MemoryPackOrder(3)]
    public virtual IdentityRole Role { get; set; } = null!;
}
