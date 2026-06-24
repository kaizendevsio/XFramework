namespace Messaging.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class Message : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string Text { get; set; } = null!;


    [MemoryPackOrder(1)]
    public Guid MessageThreadId { get; set; }

    [MemoryPackOrder(2)]
    public Guid MessageThreadMemberId { get; set; }

    [MemoryPackOrder(3)]
    public Guid? ParentMessageId { get; set; }

    [MemoryPackOrder(4)]
    public string MentionedCredentialIdsJson { get; set; } = "[]";

    [MemoryPackOrder(5)]
    public virtual ICollection<MessageDelivery> MessageDeliveries { get; set; } = new List<MessageDelivery>();

    [MemoryPackOrder(6)]
    public virtual ICollection<MessageFile> MessageFiles { get; set; } = new List<MessageFile>();

    [MemoryPackOrder(7)]
    public virtual ICollection<MessageReaction> MessageReactions { get; set; } = new List<MessageReaction>();

    [MemoryPackOrder(8)]
    public virtual MessageThread MessageThread { get; set; } = null!;

    [MemoryPackOrder(9)]
    public virtual MessageThreadMember MessageThreadMember { get; set; } = null!;

    [MemoryPackOrder(10)]
    public virtual Message? ParentMessage { get; set; }

    [MemoryPackOrder(11)]
    public virtual ICollection<Message> Replies { get; set; } = new List<Message>();
}
