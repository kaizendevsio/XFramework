

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Medicine;

public class MedicineResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long EntityId { get; set; }
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? ScientificName { get; set; }
    public string? Description { get; set; }
    public string? ChemicalComponent { get; set; }
    public string Guid { get; set; } = null!;
    
    public MedicineEntityResponse Entity { get; set; } = null!;
}