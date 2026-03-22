using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using XFramework.Domain.Shared.Attributes;

namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/storage-files",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "storage-files"
)]
public partial class StorageFile : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string ContentPath { get; set; } = null!;

    [MemoryPackOrder(1)]
    public Guid TypeId { get; set; }
    
    [MemoryPackOrder(2)]
    public Guid Identifier { get; set; }

    [MemoryPackOrder(3)]
    public decimal? FileSize { get; set; }

    [MemoryPackOrder(4)]
    public DateTime? ExpireAt { get; set; }

    [MemoryPackOrder(5)]
    public Guid StorageFileIdentifierId { get; set; }

    [MemoryPackOrder(6)]
    public string? Hash { get; set; }

    [MemoryPackOrder(7)]
    public string? Name { get; set; }

    [MemoryPackOrder(8)]
    public string? ContentType { get; set; }
    
    [MemoryPackOrder(9)]
    public string? BlobContainer { get; set; }

    [NotMapped]
    [JsonIgnore]
    [MemoryPackOrder(10)]
    public byte[]? FileBytes { get; set; }

    [MemoryPackOrder(13)]
    public virtual StorageFileType Type { get; set; } = null!;

    [MemoryPackOrder(15)]
    public virtual StorageFileIdentifier? StorageFileIdentifier { get; set; }
}

public class CreateStorageFileRequest
{
    public string ContentPath { get; set; } = null!;
    public Guid TypeId { get; set; }
    public Guid Identifier { get; set; }
    public decimal? FileSize { get; set; }
    public DateTime? ExpireAt { get; set; }
    public Guid StorageFileIdentifierId { get; set; }
    public string? Hash { get; set; }
    public string? Name { get; set; }
    public string? ContentType { get; set; }
    public string? BlobContainer { get; set; }
}

public class UpdateStorageFileRequest
{
    public string ContentPath { get; set; } = null!;
    public Guid TypeId { get; set; }
    public Guid Identifier { get; set; }
    public decimal? FileSize { get; set; }
    public DateTime? ExpireAt { get; set; }
    public Guid StorageFileIdentifierId { get; set; }
    public string? Hash { get; set; }
    public string? Name { get; set; }
    public string? ContentType { get; set; }
    public string? BlobContainer { get; set; }
}

public class GetStorageFileListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? TypeId { get; set; }
    public Guid? Identifier { get; set; }
    public Guid? StorageFileIdentifierId { get; set; }
}
