namespace HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Create;

public class CreateDoctorTagRequest : RequestBase
{
    public Guid? DoctorGuid { get; set; }
    public string? Value { get; set; }
    public Guid? TagGuid { get; set; }
}