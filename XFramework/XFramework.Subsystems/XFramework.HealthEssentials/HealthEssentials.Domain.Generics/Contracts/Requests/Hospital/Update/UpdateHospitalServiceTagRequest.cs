namespace HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Update;

public class UpdateHospitalServiceTagRequest : RequestBase
{
    public Guid HospitalServiceGuid { get; set; }
    public string? Value { get; set; }
    public Guid? TagGuid { get; set; }
}