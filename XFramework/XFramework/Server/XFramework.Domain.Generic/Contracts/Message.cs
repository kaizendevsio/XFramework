namespace XFramework.Domain.Generic.Contracts;

public partial class Message : BaseModel
{
    public string Text { get; set; } = null!;


    public Guid MessageThreadId { get; set; }

    public Guid MessageThreadMemberId { get; set; }

    public virtual ICollection<MessageDelivery> MessageDeliveries { get; } = new List<MessageDelivery>();

    public virtual ICollection<MessageFile> MessageFiles { get; } = new List<MessageFile>();

    public virtual ICollection<MessageReaction> MessageReactions { get; } = new List<MessageReaction>();

    public virtual MessageThread MessageThread { get; set; } = null!;

    public virtual MessageThreadMember MessageThreadMember { get; set; } = null!;
}