using HealthEssentials.Domain.Generics.Enums;
using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Get;

public class GetLogisticJobOrderListRequest : QueryableRequest
{
    public TransactionStatus RecordType { get; set; }
    public Guid? RiderGuid { get; set; }
}