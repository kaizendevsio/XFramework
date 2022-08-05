using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalHandler : QueryBaseHandler, IRequestHandler<GetHospitalQuery, QueryResponse<HospitalResponse>>
{
    public GetHospitalHandler()
    {
        
    }
    public async Task<QueryResponse<HospitalResponse>> Handle(GetHospitalQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}