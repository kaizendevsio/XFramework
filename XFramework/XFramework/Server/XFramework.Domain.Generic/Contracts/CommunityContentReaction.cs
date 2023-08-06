namespace XFramework.Domain.Generic.Contracts;

public partial class CommunityContentReaction
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid ContentId { get; set; }

    public Guid TypeId { get; set; }

    public Guid SocialMediaIdentityId { get; set; }

    
    public virtual CommunityContent Content { get; set; } = null!;

    public virtual CommunityContentReactionType Type { get; set; } = null!;

    public virtual CommunityIdentity SocialMediaIdentity { get; set; } = null!;
}
