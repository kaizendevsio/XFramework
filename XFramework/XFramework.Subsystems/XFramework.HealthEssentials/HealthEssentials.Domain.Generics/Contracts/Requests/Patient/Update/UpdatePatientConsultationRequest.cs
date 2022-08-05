namespace HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Update;

public class UpdatePatientConsultationRequest : RequestBase
{
    public Guid? PatientGuid { get; set; }
    public Guid? ConsultationJobOrderGuid { get; set; }
}