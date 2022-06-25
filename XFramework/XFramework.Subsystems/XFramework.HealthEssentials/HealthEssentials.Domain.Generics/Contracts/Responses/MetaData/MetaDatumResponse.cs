

namespace HealthEssentials.Domain.Generics.Contracts.Responses.MetaData;

public class MetaDatumResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long? KeyId { get; set; }
    public string? Value { get; set; }
    public Guid? Guid { get; set; }
    
    public MetaDataEntityResponse? Entity { get; set; }
}