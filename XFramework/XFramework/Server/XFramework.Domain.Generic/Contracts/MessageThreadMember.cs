namespace XFramework.Domain.Generic.Contracts;

public partial class MessageThreadMember
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public short Status { get; set; }

    public string Emoji { get; set; } = null!;

    public string Alias { get; set; } = null!;

    public string Description { get; set; } = null!;

    
    public Guid MessageThreadId { get; set; }

    public Guid CredentialId { get; set; }

    public Guid GroupId { get; set; }

    public virtual MessageThreadMemberGroup Group { get; set; } = null!;

    public virtual IdentityCredential Credential { get; set; } = null!;

    public virtual ICollection<MessageDelivery> MessageDeliveries { get; } = new List<MessageDelivery>();

    public virtual MessageThread MessageThread { get; set; } = null!;

    public virtual ICollection<MessageThreadMemberRole> MessageThreadMemberRoles { get; } = new List<MessageThreadMemberRole>();

    public virtual ICollection<Message> Messages { get; } = new List<Message>();
}
