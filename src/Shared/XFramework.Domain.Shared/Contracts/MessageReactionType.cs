namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class MessageReactionType : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string Name { get; set; } = null!;

    [MemoryPackOrder(1)]
    public string Emoji { get; set; } = null!;

    [MemoryPackOrder(2)]
    public virtual ICollection<MessageReaction> MessageReactions { get; set; } = new List<MessageReaction>();
}
