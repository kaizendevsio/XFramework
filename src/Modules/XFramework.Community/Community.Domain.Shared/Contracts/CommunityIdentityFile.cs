using XFramework.Domain.Shared.Attributes;

namespace Community.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/community-identity-files",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "community-identity-files"
)]
public partial class CommunityIdentityFile : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid IdentityId { get; set; }

    [MemoryPackOrder(1)]
    public Guid StorageId { get; set; }

    [MemoryPackOrder(2)]
    public Guid TypeId { get; set; }

    [MemoryPackOrder(3)]
    public virtual CommunityIdentityFileType Type { get; set; } = null!;

    [MemoryPackOrder(4)]
    public virtual CommunityIdentity Identity { get; set; } = null!;

    [MemoryPackOrder(5)]
    public virtual StorageFile Storage { get; set; } = null!;
}

public class CreateCommunityIdentityFileRequest
{
    public Guid IdentityId { get; set; }
    public Guid StorageId { get; set; }
    public Guid TypeId { get; set; }
}

public class UpdateCommunityIdentityFileRequest
{
    public Guid IdentityId { get; set; }
    public Guid StorageId { get; set; }
    public Guid TypeId { get; set; }
}

public class GetCommunityIdentityFileListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? IdentityId { get; set; }
    public Guid? TypeId { get; set; }
}
