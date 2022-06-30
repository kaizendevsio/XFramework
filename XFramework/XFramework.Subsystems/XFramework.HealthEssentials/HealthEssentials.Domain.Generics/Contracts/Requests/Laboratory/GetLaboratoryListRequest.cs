using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory;

public class GetLaboratoryListRequest : QueryableRequest
{
    public GenericStatusType Status { get; set; }
}