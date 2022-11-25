using HealthEssentials.Domain.Generics.Enums;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Get;

public class GetPharmacyJobOrderConsultationJobOrderListRequest : QueryableRequest
{
    public TransactionStatus Status { get; set; }
}