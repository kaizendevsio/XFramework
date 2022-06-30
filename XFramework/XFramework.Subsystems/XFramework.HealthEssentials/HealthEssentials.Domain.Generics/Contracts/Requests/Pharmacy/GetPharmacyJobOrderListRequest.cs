using HealthEssentials.Domain.Generics.Enums;
using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy;

public class GetPharmacyJobOrderListRequest : QueryableRequest
{
    public TransactionRecordType RecordType { get; set; }
    public Guid? PharmacyLocationGuid { get; set; }
}