namespace XFramework.Domain.Generic.Contracts;

public partial record CommunityConnection : BaseModel
{
    public Guid SourceSocialMediaIdentityId { get; set; }

    public Guid TargetSocialMediaIdentityId { get; set; }

    public Guid TypeId { get; set; }


    public virtual CommunityConnectionType Type { get; set; } = null!;

    public virtual CommunityIdentity SourceSocialMediaIdentity { get; set; } = null!;

    public virtual CommunityIdentity TargetSocialMediaIdentity { get; set; } = null!;
}