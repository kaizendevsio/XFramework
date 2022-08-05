namespace HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Create;

public class CreatePatientTagRequest : RequestBase
{
    public Guid? PatientGuid { get; set; }
    public string? Value { get; set; }
    public Guid? TagGuid { get; set; }
}