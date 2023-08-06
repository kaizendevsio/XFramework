namespace XFramework.Domain.Generic.Contracts;

public partial class Message
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public string Text { get; set; } = null!;

    
    public long MessageThreadId { get; set; }

    public long MessageThreadMemberId { get; set; }

    public virtual ICollection<MessageDelivery> MessageDeliveries { get; } = new List<MessageDelivery>();

    public virtual ICollection<MessageFile> MessageFiles { get; } = new List<MessageFile>();

    public virtual ICollection<MessageReaction> MessageReactions { get; } = new List<MessageReaction>();

    public virtual MessageThread MessageThread { get; set; } = null!;

    public virtual MessageThreadMember MessageThreadMember { get; set; } = null!;
}
