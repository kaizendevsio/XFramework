using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyMemberListHandler : QueryBaseHandler, IRequestHandler<GetPharmacyMemberListQuery, QueryResponse<List<PharmacyMemberResponse>>>
{
    public GetPharmacyMemberListHandler()
    {
        
    }
    public async Task<QueryResponse<List<PharmacyMemberResponse>>> Handle(GetPharmacyMemberListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}