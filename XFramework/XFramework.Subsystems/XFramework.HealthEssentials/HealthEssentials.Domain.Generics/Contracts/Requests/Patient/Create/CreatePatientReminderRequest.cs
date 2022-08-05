namespace HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Create;

public class CreatePatientReminderRequest : RequestBase
{
    public Guid? PatientGuid { get; set; }
    public Guid? ConsultationJobOrderGuid { get; set; }
    public bool IsSeen { get; set; }
    public short Status { get; set; }
}