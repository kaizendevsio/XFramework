using HealthEssentials.Domain.Generics.Enums;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Get;

public class GetLaboratoryJobOrderDetailListRequest : QueryableRequest
{
    public TransactionRecordType Status { get; set; }
}