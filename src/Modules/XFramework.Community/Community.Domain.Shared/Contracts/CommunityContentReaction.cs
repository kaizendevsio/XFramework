using XFramework.Domain.Shared.Attributes;

namespace Community.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/community-content-reactions",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "community-content-reactions"
)]
public partial class CommunityContentReaction : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid ContentId { get; set; }

    [MemoryPackOrder(1)]
    public Guid TypeId { get; set; }

    [MemoryPackOrder(2)]
    public Guid SocialMediaIdentityId { get; set; }


    [MemoryPackOrder(3)]
    public virtual CommunityContent Content { get; set; } = null!;

    [MemoryPackOrder(4)]
    public virtual CommunityContentReactionType Type { get; set; } = null!;

    [MemoryPackOrder(5)]
    public virtual CommunityIdentity SocialMediaIdentity { get; set; } = null!;
}

public class CreateCommunityContentReactionRequest
{
    public Guid ContentId { get; set; }
    public Guid TypeId { get; set; }
    public Guid SocialMediaIdentityId { get; set; }
}

public class UpdateCommunityContentReactionRequest
{
    public Guid ContentId { get; set; }
    public Guid TypeId { get; set; }
    public Guid SocialMediaIdentityId { get; set; }
}

public class GetCommunityContentReactionListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? ContentId { get; set; }
    public Guid? TypeId { get; set; }
    public Guid? SocialMediaIdentityId { get; set; }
}
