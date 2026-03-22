using XFramework.Domain.Shared.Attributes;

namespace Community.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/community-content-files",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "community-content-files"
)]
public partial class CommunityContentFile : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid ContentId { get; set; }

    [MemoryPackOrder(1)]
    public Guid StorageId { get; set; }


    [MemoryPackOrder(2)]
    public virtual CommunityContent Content { get; set; } = null!;

    [MemoryPackOrder(3)]
    public virtual StorageFile Storage { get; set; } = null!;
}

public class CreateCommunityContentFileRequest
{
    public Guid ContentId { get; set; }
    public Guid StorageId { get; set; }
}

public class UpdateCommunityContentFileRequest
{
    public Guid ContentId { get; set; }
    public Guid StorageId { get; set; }
}

public class GetCommunityContentFileListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? ContentId { get; set; }
}
