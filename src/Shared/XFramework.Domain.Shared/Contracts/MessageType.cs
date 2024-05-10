namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class MessageType : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string Name { get; set; } = null!;

    [MemoryPackOrder(1)]
    public short Priority { get; set; }


    [MemoryPackOrder(2)]
    public virtual ICollection<MessageDirect> MessageDirects { get; set; } = new List<MessageDirect>();

    [MemoryPackOrder(3)]
    public virtual ICollection<MessageThreadType> MessageThreadTypes { get; set; } = new List<MessageThreadType>();
}
