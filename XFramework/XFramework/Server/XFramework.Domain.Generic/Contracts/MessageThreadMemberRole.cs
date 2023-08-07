namespace XFramework.Domain.Generic.Contracts;

public partial class MessageThreadMemberRole
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    
    public Guid MessageThreadMemberId { get; set; }

    public Guid RoleId { get; set; }

    public virtual MessageThreadMember MessageThreadMember { get; set; } = null!;

    public virtual IdentityRole Role { get; set; } = null!;
}
