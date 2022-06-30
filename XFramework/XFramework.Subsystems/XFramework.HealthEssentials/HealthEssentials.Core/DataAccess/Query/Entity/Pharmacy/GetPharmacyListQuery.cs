namespace HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

public class GetPharmacyListQuery : GetPharmacyListRequest, IRequest<QueryResponse<List<PharmacyResponse>>>
{
    
}