namespace XFramework.Domain.Generic.Contracts;

public partial record CommunityContentReaction : BaseModel
{
    public Guid ContentId { get; set; }

    public Guid TypeId { get; set; }

    public Guid SocialMediaIdentityId { get; set; }


    public virtual CommunityContent Content { get; set; } = null!;

    public virtual CommunityContentReactionType Type { get; set; } = null!;

    public virtual CommunityIdentity SocialMediaIdentity { get; set; } = null!;
}