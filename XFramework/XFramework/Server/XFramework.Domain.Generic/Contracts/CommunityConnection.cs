namespace XFramework.Domain.Generic.Contracts;

public partial class CommunityConnection
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid SourceSocialMediaIdentityId { get; set; }

    public Guid TargetSocialMediaIdentityId { get; set; }

    public Guid TypeId { get; set; }

    
    public virtual CommunityConnectionType Type { get; set; } = null!;

    public virtual CommunityIdentity SourceSocialMediaIdentity { get; set; } = null!;

    public virtual CommunityIdentity TargetSocialMediaIdentity { get; set; } = null!;
}
