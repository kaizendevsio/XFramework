namespace HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Update;

public class UpdateDoctorTagRequest : RequestBase
{
    public Guid? DoctorGuid { get; set; }
    public string? Value { get; set; }
    public Guid? TagGuid { get; set; }
}