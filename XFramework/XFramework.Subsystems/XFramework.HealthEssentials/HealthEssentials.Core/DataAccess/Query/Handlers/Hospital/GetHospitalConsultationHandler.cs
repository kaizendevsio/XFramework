using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalConsultationHandler : QueryBaseHandler, IRequestHandler<GetHospitalConsultationQuery, QueryResponse<HospitalConsultationResponse>>
{
    public GetHospitalConsultationHandler()
    {
        
    }
    public async Task<QueryResponse<HospitalConsultationResponse>> Handle(GetHospitalConsultationQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}