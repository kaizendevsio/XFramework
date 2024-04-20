using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class IdentityRole : BaseModel
{
    public Guid CredentialId { get; set; }

    public Guid? TypeId { get; set; }

    public DateTime RoleExpiration { get; set; }
    
    public virtual ICollection<MessageThreadMemberRole> MessageThreadMemberRoles { get; set; } =
        new List<MessageThreadMemberRole>();

    public virtual IdentityRoleType? Type { get; set; }

    public virtual IdentityCredential Credential { get; set; } = null!;
}