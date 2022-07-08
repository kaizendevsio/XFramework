using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Create;

public class CreateDoctorConsultationJobOrderRequest : RequestBase
{
    public Guid? DoctorGuid { get; set; }
    public Guid? ConsultationJobOrderGuid { get; set; }
}