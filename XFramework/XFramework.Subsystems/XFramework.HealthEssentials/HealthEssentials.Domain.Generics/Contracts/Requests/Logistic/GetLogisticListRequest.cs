using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Logistic;

public class GetLogisticListRequest : QueryableRequest
{
    public GenericStatusType Status { get; set; }
}