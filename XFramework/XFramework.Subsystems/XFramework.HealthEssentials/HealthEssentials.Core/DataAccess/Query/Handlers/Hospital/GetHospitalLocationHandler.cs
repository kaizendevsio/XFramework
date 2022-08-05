using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalLocationHandler : QueryBaseHandler, IRequestHandler<GetHospitalLocationQuery, QueryResponse<HospitalLocationResponse>>
{
    public GetHospitalLocationHandler()
    {
        
    }
    public async Task<QueryResponse<HospitalLocationResponse>> Handle(GetHospitalLocationQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}