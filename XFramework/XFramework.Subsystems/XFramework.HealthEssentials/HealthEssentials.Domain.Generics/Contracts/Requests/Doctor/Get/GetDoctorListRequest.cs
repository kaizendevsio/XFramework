using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Get;

public class GetDoctorListRequest : QueryableRequest
{
    public GenericStatusType Status { get; set; }
}