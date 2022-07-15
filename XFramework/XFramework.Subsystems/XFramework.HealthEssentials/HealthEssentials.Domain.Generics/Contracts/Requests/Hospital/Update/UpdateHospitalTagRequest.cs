namespace HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Update;

public class UpdateHospitalTagRequest : RequestBase
{
    public Guid? HospitalGuid { get; set; }
    public string? Value { get; set; }
    public Guid? TagGuid { get; set; }
}