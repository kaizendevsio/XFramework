using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Get;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

public class GetPharmacyLocationListQuery : GetPharmacyLocationListRequest, IRequest<QueryResponse<List<PharmacyLocationResponse>>>
{
    
}