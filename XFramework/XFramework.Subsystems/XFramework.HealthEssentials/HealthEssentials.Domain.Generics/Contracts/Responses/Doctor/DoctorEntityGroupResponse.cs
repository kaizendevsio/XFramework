namespace HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;

public class DoctorEntityGroupResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Name { get; set; }
    public Guid? Guid { get; set; }
}