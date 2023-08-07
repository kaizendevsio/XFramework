namespace XFramework.Domain.Generic.Contracts;

public partial class MessageThreadMemberGroup
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

    public virtual MessageThread MessageThread { get; set; } = null!;

    public virtual ICollection<MessageThreadMember> MessageThreadMembers { get; } = new List<MessageThreadMember>();
}
