using HealthEssentials.Domain.Generics.Contracts.Responses.Tag;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Laboratory;

public class LaboratoryServiceTagResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Value { get; set; }
    public string Guid { get; set; } = null!;

    public LaboratoryServiceResponse? LaboratoryService { get; set; }
    public TagResponse? Tag { get; set; }
}