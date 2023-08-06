namespace XFramework.Domain.Generic.Contracts;

public partial class MessageThread
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;

    
    public Guid TypeId { get; set; }

    public virtual MessageThreadType Type { get; set; } = null!;

    public virtual ICollection<MessageThreadMemberGroup> MessageThreadMemberGroups { get; } = new List<MessageThreadMemberGroup>();

    public virtual ICollection<MessageThreadMember> MessageThreadMembers { get; } = new List<MessageThreadMember>();

    public virtual ICollection<Message> Messages { get; } = new List<Message>();
}
