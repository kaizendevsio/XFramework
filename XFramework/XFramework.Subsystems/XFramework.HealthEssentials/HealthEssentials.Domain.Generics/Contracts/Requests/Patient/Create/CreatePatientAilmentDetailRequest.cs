namespace HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Create;

public class CreatePatientAilmentDetailRequest : RequestBase
{
    public Guid? PatientAilmentGuid { get; set; }
    public Guid? ConsultationJobOrderGuid { get; set; }
    public string? DoctorName { get; set; }
    public short? Status { get; set; }
    public string? Remarks { get; set; }
}