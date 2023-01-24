using HealthEssentials.Domain.Generics.Enums;
using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Get;

public class GetPharmacyJobOrderListRequest : QueryableRequest
{
    public List<TransactionStatus> Status { get; set; }
    public Guid? PharmacyLocationGuid { get; set; }
}