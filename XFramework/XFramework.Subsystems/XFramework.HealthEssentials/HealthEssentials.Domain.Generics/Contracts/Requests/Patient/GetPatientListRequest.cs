using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Patient;

public class GetPatientListRequest : QueryableRequest
{
    public Guid? DoctorGuid { get; set; }
}