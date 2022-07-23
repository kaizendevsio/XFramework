namespace HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Update;

public class UpdatePatientReminderRequest : RequestBase
{
    public Guid? PatientGuid { get; set; }
    public Guid? ConsultationJobOrderGuid { get; set; }
    public bool IsSeen { get; set; }
    public short Status { get; set; }
}