using HealthEssentials.Domain.Generics.Contracts.Responses.Tag;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Laboratory;

public class LaboratoryTagResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long LaboratoryId { get; set; }
    public string? Value { get; set; }
    public long? TagId { get; set; }
    public Guid? Guid { get; set; }

    public LaboratoryResponse? Laboratory { get; set; }
    public TagResponse? Tag { get; set; }
    
}