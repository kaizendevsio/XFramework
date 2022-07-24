using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Get;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

public class GetPharmacyTagListQuery : GetPharmacyTagListRequest, IRequest<QueryResponse<List<PharmacyTagResponse>>>
{
    
}