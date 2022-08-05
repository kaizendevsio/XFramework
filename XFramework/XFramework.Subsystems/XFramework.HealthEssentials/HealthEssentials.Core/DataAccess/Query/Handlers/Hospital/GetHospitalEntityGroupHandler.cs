using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalEntityGroupHandler : QueryBaseHandler, IRequestHandler<GetHospitalEntityGroupQuery, QueryResponse<HospitalEntityGroupResponse>>
{
    public GetHospitalEntityGroupHandler()
    {
        
    }
    public async Task<QueryResponse<HospitalEntityGroupResponse>> Handle(GetHospitalEntityGroupQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}