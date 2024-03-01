using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace XFramework.Domain.Generic.Contracts;

public partial class StorageFile : BaseModel
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

    [NotMapped]
    [JsonIgnore]
    public byte[]? FileBytes { get; set; }

    public virtual ICollection<CommunityContentFile> CommunityContentFiles { get; set; } = new List<CommunityContentFile>();

    public virtual ICollection<CommunityIdentityFile> CommunityIdentityFiles { get; set; } = new List<CommunityIdentityFile>();

    public virtual StorageFileType Type { get; set; } = null!;

    public virtual ICollection<MessageFile> MessageFiles { get; set; } = new List<MessageFile>();

    public virtual StorageFileIdentifier? StorageFileIdentifier { get; set; }
}