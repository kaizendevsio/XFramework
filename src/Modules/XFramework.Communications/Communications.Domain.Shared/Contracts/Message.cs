namespace Communications.Domain.Shared.Contracts;


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
    public Guid? TemplateId { get; set; }

    [MemoryPackOrder(6)]
    public string? TemplateKey { get; set; }

    [MemoryPackOrder(7)]
    public string? TemplateType { get; set; }

    [MemoryPackOrder(8)]
    public string TemplateVariablesJson { get; set; } = "{}";

    [MemoryPackOrder(9)]
    public virtual ICollection<MessageDelivery> MessageDeliveries { get; set; } = new List<MessageDelivery>();

    [MemoryPackOrder(10)]
    public virtual ICollection<MessageFile> MessageFiles { get; set; } = new List<MessageFile>();

    [MemoryPackOrder(11)]
    public virtual ICollection<MessageReaction> MessageReactions { get; set; } = new List<MessageReaction>();

    [MemoryPackOrder(12)]
    public virtual MessageThread MessageThread { get; set; } = null!;

    [MemoryPackOrder(13)]
    public virtual MessageThreadMember MessageThreadMember { get; set; } = null!;

    [MemoryPackOrder(14)]
    public virtual Message? ParentMessage { get; set; }

    [MemoryPackOrder(15)]
    public virtual ICollection<Message> Replies { get; set; } = new List<Message>();
}
