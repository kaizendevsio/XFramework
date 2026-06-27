namespace Communications.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class MessageReaction : BaseModel
{

    [MemoryPackOrder(0)]
    public Guid MessageId { get; set; }

    [MemoryPackOrder(1)]
    public Guid TypeId { get; set; }

    [MemoryPackOrder(2)]
    public virtual MessageReactionType Type { get; set; } = null!;

    [MemoryPackOrder(3)]
    public virtual Message Message { get; set; } = null!;

    [MemoryPackOrder(4)]
    public Guid MessageThreadMemberId { get; set; }

    [MemoryPackOrder(5)]
    public virtual MessageThreadMember MessageThreadMember { get; set; } = null!;
}
