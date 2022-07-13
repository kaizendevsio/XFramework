using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalLocationListHandler : QueryBaseHandler, IRequestHandler<GetHospitalLocationListQuery, QueryResponse<List<HospitalLocationResponse>>>
{
    public GetHospitalLocationListHandler()
    {
        
    }
    public async Task<QueryResponse<List<HospitalLocationResponse>>> Handle(GetHospitalLocationListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}