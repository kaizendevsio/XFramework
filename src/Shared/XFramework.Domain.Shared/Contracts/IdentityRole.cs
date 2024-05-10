namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class IdentityRole : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid CredentialId { get; set; }

    [MemoryPackOrder(1)]
    public Guid? TypeId { get; set; }

    [MemoryPackOrder(2)]
    public DateTime RoleExpiration { get; set; }
    
    [MemoryPackOrder(3)]
    public virtual ICollection<MessageThreadMemberRole> MessageThreadMemberRoles { get; set; } =
        new List<MessageThreadMemberRole>();

    [MemoryPackOrder(4)]
    public virtual IdentityRoleType? Type { get; set; }

    [MemoryPackOrder(5)]
    public virtual IdentityCredential Credential { get; set; } = null!;
}
