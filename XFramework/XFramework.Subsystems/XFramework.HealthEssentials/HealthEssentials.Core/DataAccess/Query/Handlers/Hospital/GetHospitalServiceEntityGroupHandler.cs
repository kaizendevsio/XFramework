using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalServiceEntityGroupHandler : QueryBaseHandler, IRequestHandler<GetHospitalServiceEntityGroupQuery, QueryResponse<HospitalServiceEntityGroupResponse>>
{
    public GetHospitalServiceEntityGroupHandler()
    {
        
    }
    public async Task<QueryResponse<HospitalServiceEntityGroupResponse>> Handle(GetHospitalServiceEntityGroupQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}