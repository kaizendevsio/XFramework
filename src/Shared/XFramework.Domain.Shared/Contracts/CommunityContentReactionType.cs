namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class CommunityContentReactionType : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string Name { get; set; } = null!;

    [MemoryPackOrder(1)]
    public string Emoji { get; set; } = null!;


    [MemoryPackOrder(2)]
    public virtual ICollection<CommunityContentReaction> CommunityContentReactions { get; set; } =
        new List<CommunityContentReaction>();
}
