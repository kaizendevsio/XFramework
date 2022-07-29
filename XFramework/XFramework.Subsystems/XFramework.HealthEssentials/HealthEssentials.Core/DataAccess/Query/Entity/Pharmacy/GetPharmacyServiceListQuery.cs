using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Get;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

public class GetPharmacyServiceListQuery : GetPharmacyServiceListRequest, IRequest<QueryResponse<List<PharmacyServiceResponse>>>
{
    
}