namespace HealthEssentials.Domain.Generics.Contracts.Responses.Storage;

public class StorageFileResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string ContentPath { get; set; } = null!;
    public long EntityId { get; set; }
    public string Guid { get; set; } = null!;
    public string? IdentifierGuid { get; set; }
    public decimal? FileSize { get; set; }
    public DateTime? ExpireAt { get; set; }
    public long? StorageFileIdentifierId { get; set; }
    public string? Hash { get; set; }
}