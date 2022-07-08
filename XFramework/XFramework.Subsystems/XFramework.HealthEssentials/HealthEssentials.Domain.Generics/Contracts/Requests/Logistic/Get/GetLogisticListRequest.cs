using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Get;

public class GetLogisticListRequest : QueryableRequest
{
    public GenericStatusType Status { get; set; }
}