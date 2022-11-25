namespace HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Create;

public class CreatePatientConsultationRequest : RequestBase
{
    public Guid? PatientGuid { get; set; }
    public Guid? ConsultationJobOrderGuid { get; set; }
}