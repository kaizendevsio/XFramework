using XFramework.Domain.Shared.Attributes;

namespace Community.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.ReadOnly,
    RoutePrefix = "api/community-content-reaction-types",
    RequireAuthorization = true,
    CacheDurationSeconds = 3600,
    CacheKeyPrefix = "community-content-reaction-types"
)]
public partial class CommunityContentReactionType : BaseModel, IHasSystemReferenceId
{
    
    [MemoryPackOrder(0)]
    public string Name { get; set; } = null!;

    [MemoryPackOrder(1)]
    public string Emoji { get; set; } = null!;


    [MemoryPackOrder(2)]
    public virtual ICollection<CommunityContentReaction> CommunityContentReactions { get; set; } =
        new List<CommunityContentReaction>();

    [MemoryPackOrder(200)]
    public Guid SystemReferenceId { get; set; }
}

public class GetCommunityContentReactionTypeListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public string? Name { get; set; }
}
