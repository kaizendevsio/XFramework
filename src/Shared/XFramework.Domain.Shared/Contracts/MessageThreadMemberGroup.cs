namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class MessageThreadMemberGroup : BaseModel, IHasSystemReferenceId
{
    
    [MemoryPackOrder(0)]
    public short Status { get; set; }

    [MemoryPackOrder(1)]
    public string Emoji { get; set; } = null!;

    [MemoryPackOrder(2)]
    public string Alias { get; set; } = null!;

    [MemoryPackOrder(3)]
    public string Description { get; set; } = null!;


    [MemoryPackOrder(4)]
    public Guid MessageThreadId { get; set; }

    [MemoryPackOrder(5)]
    public virtual MessageThread MessageThread { get; set; } = null!;

    [MemoryPackOrder(6)]
    public virtual ICollection<MessageThreadMember> MessageThreadMembers { get; set; } = new List<MessageThreadMember>();

    [MemoryPackOrder(200)]
    public Guid SystemReferenceId { get; set; }
}
