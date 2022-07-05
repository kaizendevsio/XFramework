using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Get;

public class GetSupportedConsultationListRequest : QueryableRequest
{
    public Guid? DoctorGuid { get; set; }
}