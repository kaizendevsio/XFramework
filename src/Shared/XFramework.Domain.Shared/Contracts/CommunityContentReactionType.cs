using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class CommunityContentReactionType : BaseModel
{
    public string Name { get; set; } = null!;

    public string Emoji { get; set; } = null!;


    public virtual ICollection<CommunityContentReaction> CommunityContentReactions { get; set; } =
        new List<CommunityContentReaction>();
}