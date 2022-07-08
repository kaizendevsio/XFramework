using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Get;

public class GetLaboratoryLocationListRequest : QueryableRequest
{
    public Guid? LaboratoryGuid { get; set; }
}