using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalServiceHandler : QueryBaseHandler, IRequestHandler<GetHospitalServiceQuery, QueryResponse<HospitalServiceResponse>>
{
    public GetHospitalServiceHandler()
    {
        
    }
    public async Task<QueryResponse<HospitalServiceResponse>> Handle(GetHospitalServiceQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}