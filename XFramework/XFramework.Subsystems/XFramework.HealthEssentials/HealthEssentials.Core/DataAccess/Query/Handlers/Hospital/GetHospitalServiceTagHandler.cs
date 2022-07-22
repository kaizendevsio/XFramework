using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalServiceTagHandler : QueryBaseHandler, IRequestHandler<GetHospitalServiceTagQuery, QueryResponse<HospitalServiceTagResponse>>
{
    public GetHospitalServiceTagHandler()
    {
        
    }
    public async Task<QueryResponse<HospitalServiceTagResponse>> Handle(GetHospitalServiceTagQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}