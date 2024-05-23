namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class MessageThreadType : BaseModel, IHasSystemReferenceId
{
    
    [MemoryPackOrder(0)]
    public string Name { get; set; } = null!;


    [MemoryPackOrder(1)]
    public Guid MessageTypeId { get; set; }

    [MemoryPackOrder(2)]
    public virtual ICollection<MessageThread> MessageThreads { get; set; } = new List<MessageThread>();

    [MemoryPackOrder(3)]
    public virtual MessageType MessageType { get; set; } = null!;

    [MemoryPackOrder(200)]
    public Guid SystemReferenceId { get; set; }
}
