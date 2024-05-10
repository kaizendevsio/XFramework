namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class MessageDelivery : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid MessageThreadMemberId { get; set; }

    [MemoryPackOrder(1)]
    public Guid MessageId { get; set; }

    [MemoryPackOrder(2)]
    public Guid TypeId { get; set; }

    [MemoryPackOrder(3)]
    public virtual MessageDeliveryType Type { get; set; } = null!;

    [MemoryPackOrder(4)]
    public virtual Message Message { get; set; } = null!;

    [MemoryPackOrder(5)]
    public virtual MessageThreadMember MessageThreadMember { get; set; } = null!;
}
