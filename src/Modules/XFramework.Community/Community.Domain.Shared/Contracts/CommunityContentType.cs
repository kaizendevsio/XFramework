using XFramework.Domain.Shared.Attributes;

namespace Community.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.ReadOnly,
    RoutePrefix = "api/community-content-types",
    RequireAuthorization = true,
    CacheDurationSeconds = 3600,
    CacheKeyPrefix = "community-content-types"
)]
public partial class CommunityContentType : BaseModel, IHasSystemReferenceId
{
    
    [MemoryPackOrder(0)]
    public string Name { get; set; } = null!;


    [MemoryPackOrder(1)]
    public virtual ICollection<CommunityContent> CommunityContents { get; set; } = new List<CommunityContent>();

    [MemoryPackOrder(200)]
    public Guid SystemReferenceId { get; set; }
}

public class GetCommunityContentTypeListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public string? Name { get; set; }
}
