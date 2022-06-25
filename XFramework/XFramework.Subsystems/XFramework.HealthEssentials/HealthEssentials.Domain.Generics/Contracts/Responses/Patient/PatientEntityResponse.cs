

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Patient;

public class PatientEntityResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid? Guid { get; set; }
    public int? SortOrder { get; set; }

    public PatientEntityGroupResponse? Group { get; set; }
}