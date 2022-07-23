namespace HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Update;

public class UpdatePatientTagRequest : RequestBase
{
    public Guid? PatientGuid { get; set; }
    public string? Value { get; set; }
    public Guid? TagGuid { get; set; }
}