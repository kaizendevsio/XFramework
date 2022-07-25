using HealthEssentials.Domain.Generics.Enums;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Get;

public class GetPharmacyJobOrderMedicineListRequest : QueryableRequest
{
    public TransactionRecordType Status { get; set; }
}