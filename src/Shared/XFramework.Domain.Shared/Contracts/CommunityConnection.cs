using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class CommunityConnection : BaseModel
{
    public Guid SourceSocialMediaIdentityId { get; set; }

    public Guid TargetSocialMediaIdentityId { get; set; }

    public Guid TypeId { get; set; }


    public virtual CommunityConnectionType Type { get; set; } = null!;

    public virtual CommunityIdentity SourceSocialMediaIdentity { get; set; } = null!;

    public virtual CommunityIdentity TargetSocialMediaIdentity { get; set; } = null!;
}