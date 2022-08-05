using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Get;

public class GetPharmacyLocationDocumentListRequest : QueryableRequest
{
    public Guid? PharmacyLocationGuid { get; set; }

}