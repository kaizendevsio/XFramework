using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalEntityHandler : QueryBaseHandler, IRequestHandler<GetHospitalEntityQuery, QueryResponse<HospitalEntityResponse>>
{
    public GetHospitalEntityHandler()
    {
        
    }
    public async Task<QueryResponse<HospitalEntityResponse>> Handle(GetHospitalEntityQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}