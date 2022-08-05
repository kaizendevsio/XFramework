using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalLaboratoryHandler : QueryBaseHandler, IRequestHandler<GetHospitalLaboratoryQuery, QueryResponse<HospitalLaboratoryResponse>>
{
    public GetHospitalLaboratoryHandler()
    {
        
    }
    public async Task<QueryResponse<HospitalLaboratoryResponse>> Handle(GetHospitalLaboratoryQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}