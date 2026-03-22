using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
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
