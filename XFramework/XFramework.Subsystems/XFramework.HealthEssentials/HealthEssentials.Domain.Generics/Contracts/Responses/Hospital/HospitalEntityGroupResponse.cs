namespace HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

public class HospitalEntityGroupResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string Name { get; set; } = null!;
    public Guid? Guid { get; set; }
}