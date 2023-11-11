namespace XFramework.Domain.Generic.Contracts;

public partial class CommunityContentReactionType : BaseModel
{
    public string Name { get; set; } = null!;

    public string Emoji { get; set; } = null!;


    public virtual ICollection<CommunityContentReaction> CommunityContentReactions { get; } =
        new List<CommunityContentReaction>();
}