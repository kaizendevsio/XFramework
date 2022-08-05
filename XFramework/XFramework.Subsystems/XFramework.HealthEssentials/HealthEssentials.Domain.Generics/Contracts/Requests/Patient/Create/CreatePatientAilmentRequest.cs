namespace HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Create;

public class CreatePatientAilmentRequest : RequestBase
{
    public Guid? PatientGuid { get; set; }
    public Guid? AilmentGuid { get; set; }
    public string? Remarks { get; set; }
}