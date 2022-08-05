namespace HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Update;

public class UpdatePatientAilmentRequest : RequestBase
{
    public Guid? PatientGuid { get; set; }
    public Guid? AilmentGuid { get; set; }
    public string? Remarks { get; set; }
}