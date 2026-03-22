using XFramework.Domain.Shared.Attributes;

namespace Community.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.ReadOnly,
    RoutePrefix = "api/community-identity-file-types",
    RequireAuthorization = true,
    CacheDurationSeconds = 3600,
    CacheKeyPrefix = "community-identity-file-types"
)]
public partial class CommunityIdentityFileType : BaseModel, IHasSystemReferenceId
{
    
    [MemoryPackOrder(0)]
    public string Name { get; set; } = null!;


    [MemoryPackOrder(1)]
    public virtual ICollection<CommunityIdentityFile> CommunityIdentityFiles { get; set; } =
        new List<CommunityIdentityFile>();

    [MemoryPackOrder(200)]
    public Guid SystemReferenceId { get; set; }
}

public class GetCommunityIdentityFileTypeListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public string? Name { get; set; }
}
