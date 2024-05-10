namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class MessageThread : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string Name { get; set; } = null!;

    [MemoryPackOrder(1)]
    public string Description { get; set; } = null!;


    [MemoryPackOrder(2)]
    public Guid TypeId { get; set; }

    [MemoryPackOrder(3)]
    public virtual MessageThreadType Type { get; set; } = null!;

    [MemoryPackOrder(4)]
    public virtual ICollection<MessageThreadMemberGroup> MessageThreadMemberGroups { get; set; } =
        new List<MessageThreadMemberGroup>();

    [MemoryPackOrder(5)]
    public virtual ICollection<MessageThreadMember> MessageThreadMembers { get; set; } = new List<MessageThreadMember>();

    [MemoryPackOrder(6)]
    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
}
