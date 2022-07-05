using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Get;

public class GetPatientListRequest : QueryableRequest
{
    public Guid? DoctorGuid { get; set; }
}