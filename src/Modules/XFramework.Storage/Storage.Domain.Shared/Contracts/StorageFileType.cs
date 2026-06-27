using XFramework.Domain.Shared.Attributes;

namespace XFramework.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.ReadOnly,
    RoutePrefix = "api/storage-file-types",
    RequireAuthorization = true,
    CacheDurationSeconds = 3600,
    CacheKeyPrefix = "storage-file-types"
)]
public partial class StorageFileType : BaseModel, IHasSystemReferenceId
{
    [MemoryPackOrder(0)]
    public string Name { get; set; } = string.Empty;

    [MemoryPackOrder(1)]
    public virtual ICollection<StorageFile> StorageFiles { get; set; } = new List<StorageFile>();

    [MemoryPackOrder(200)]
    public Guid SystemReferenceId { get; set; }
}

public class GetStorageFileTypeListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
}
