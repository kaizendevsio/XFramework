namespace XFramework.Domain.Generic.Contracts;

public partial class CommunityContent : BaseModel
{
    public string? Title { get; set; }

    public string? Text { get; set; }

    public Guid SocialMediaIdentityId { get; set; }

    public Guid TypeId { get; set; }

    public Guid? ParentContentId { get; set; }


    public Guid CommunityGroupId { get; set; }

    public virtual ICollection<CommunityContentFile> CommunityContentFiles { get; } = new List<CommunityContentFile>();

    public virtual ICollection<CommunityContentReaction> CommunityContentReactions { get; } =
        new List<CommunityContentReaction>();

    public virtual CommunityIdentity? CommunityGroup { get; set; }

    public virtual CommunityContentType Type { get; set; } = null!;

    public virtual ICollection<CommunityContent> InverseParentContent { get; } = new List<CommunityContent>();

    public virtual CommunityContent? ParentContent { get; set; }

    public virtual CommunityIdentity SocialMediaIdentity { get; set; } = null!;
}