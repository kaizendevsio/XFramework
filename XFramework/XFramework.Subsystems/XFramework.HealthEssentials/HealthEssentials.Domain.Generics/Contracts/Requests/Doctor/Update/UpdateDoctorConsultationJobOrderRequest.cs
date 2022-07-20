namespace HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Update;

public class UpdateDoctorConsultationJobOrderRequest : RequestBase
{
    public Guid? DoctorGuid { get; set; }
    public Guid? ConsultationJobOrderGuid { get; set; }
}