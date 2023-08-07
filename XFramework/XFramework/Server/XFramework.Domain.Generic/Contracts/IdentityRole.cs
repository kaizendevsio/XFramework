namespace XFramework.Domain.Generic.Contracts;

public partial class IdentityRole
{
    public Guid Id { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public long? ModifiedBy { get; set; }

    public bool IsDeleted { get; set; }

    public Guid CredentialId { get; set; }

    public Guid? TypeId { get; set; }

    public DateTime RoleExpiration { get; set; }

    public virtual ICollection<IncomePartition> IncomePartitions { get; } = new List<IncomePartition>();

    public virtual ICollection<MessageThreadMemberRole> MessageThreadMemberRoles { get; } = new List<MessageThreadMemberRole>();

    public virtual IdentityRoleType? Type { get; set; }

    public virtual IdentityCredential Credential { get; set; } = null!;
}
