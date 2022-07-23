using HealthEssentials.Domain.Generics.Enums;
using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Get;

public class GetLaboratoryJobOrderListRequest : QueryableRequest
{
    public TransactionRecordType Status { get; set; }
    public Guid? LaboratoryLocationGuid { get; set; }
}