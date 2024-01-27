namespace XFramework.Domain.Generic.Contracts;

public partial class IdentityRole : BaseModel
{
    public Guid CredentialId { get; set; }

    public Guid? TypeId { get; set; }

    public DateTime RoleExpiration { get; set; }
    
    public virtual ICollection<MessageThreadMemberRole> MessageThreadMemberRoles { get; } =
        new List<MessageThreadMemberRole>();

    public virtual IdentityRoleType? Type { get; set; }

    public virtual IdentityCredential Credential { get; set; } = null!;
}