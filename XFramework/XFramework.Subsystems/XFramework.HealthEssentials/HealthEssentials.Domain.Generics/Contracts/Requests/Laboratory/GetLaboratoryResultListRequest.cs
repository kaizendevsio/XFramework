using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory;

public class GetLaboratoryResultListRequest : QueryableRequest
{
    public Guid? PatientGuid { get; set; }
}