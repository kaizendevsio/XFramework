using XFramework.Domain.Shared.Attributes;

namespace Community.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.ReadOnly,
    RoutePrefix = "api/community-connections",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "community-connections"
)]
public partial class CommunityConnection : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid SourceSocialMediaIdentityId { get; set; }

    [MemoryPackOrder(1)]
    public Guid TargetSocialMediaIdentityId { get; set; }

    [MemoryPackOrder(2)]
    public Guid TypeId { get; set; }

    [MemoryPackOrder(3)]
    public virtual CommunityConnectionType Type { get; set; } = null!;

    [MemoryPackOrder(4)]
    public virtual CommunityIdentity SourceSocialMediaIdentity { get; set; } = null!;

    [MemoryPackOrder(5)]
    public virtual CommunityIdentity TargetSocialMediaIdentity { get; set; } = null!;
}

// Create/Update flows are handled by manual endpoints in Features/Connections/.
