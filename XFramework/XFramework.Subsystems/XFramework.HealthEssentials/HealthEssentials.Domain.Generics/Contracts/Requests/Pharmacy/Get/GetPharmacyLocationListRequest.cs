using HealthEssentials.Domain.Generics.Enums;
using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Get;

public class GetPharmacyLocationListRequest : QueryableRequest
{
    public TransactionStatus Status { get; set; }
}