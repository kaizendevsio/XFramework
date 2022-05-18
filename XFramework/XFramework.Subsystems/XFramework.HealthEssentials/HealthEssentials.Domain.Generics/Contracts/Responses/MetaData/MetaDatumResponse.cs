

namespace HealthEssentials.Domain.Generics.Contracts.Responses.MetaData;

public class MetaDatumResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long EntityId { get; set; }
    public long? KeyId { get; set; }
    public string? Value { get; set; }
    public string Guid { get; set; } = null!;
    
    public MetaDataEntityResponse Entity { get; set; } = null!;
}