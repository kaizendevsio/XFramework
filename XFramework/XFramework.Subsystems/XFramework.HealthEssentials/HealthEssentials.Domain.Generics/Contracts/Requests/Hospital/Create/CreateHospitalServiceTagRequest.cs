namespace HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Create;

public class CreateHospitalServiceTagRequest : RequestBase
{
    public Guid HospitalServiceGuid { get; set; }
    public string? Value { get; set; }
    public Guid? TagGuid { get; set; }
}