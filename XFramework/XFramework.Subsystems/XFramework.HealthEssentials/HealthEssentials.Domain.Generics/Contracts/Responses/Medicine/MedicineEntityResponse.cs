using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Medicine;

public class MedicineEntityResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string Name { get; set; } = null!;
    public string Guid { get; set; } = null!;
    public long GroupId { get; set; }
    public int? SortOrder { get; set; }
    
    public MedicineEntityGroup Group { get; set; } = null!;
}