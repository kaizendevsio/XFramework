namespace HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Create;

public class CreateHospitalTagRequest : RequestBase
{
    public Guid HospitalGuid { get; set; }
    public string? Value { get; set; }
    public Guid? TagGuid { get; set; }
}