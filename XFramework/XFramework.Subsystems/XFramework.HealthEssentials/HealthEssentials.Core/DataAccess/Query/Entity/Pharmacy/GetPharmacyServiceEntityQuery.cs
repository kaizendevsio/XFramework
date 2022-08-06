using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Get;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

public class GetPharmacyServiceEntityQuery : GetPharmacyServiceEntityRequest, IRequest<QueryResponse<PharmacyServiceEntityResponse>>
{
    
}