using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Doctor;

public class GetSupportedConsultationListRequest : QueryableRequest
{
    public Guid? DoctorGuid { get; set; }
}