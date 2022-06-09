using HealthEssentials.Core.DataAccess.Query.Entity.Patient;
using HealthEssentials.Domain.Generics.Contracts.Responses.Patient;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Patient;

public class GetPatientListHandler : QueryBaseHandler, IRequestHandler<GetPatientListQuery, QueryResponse<List<PatientResponse>>>
{
    public GetPatientListHandler()
    {
        
    }
    public async Task<QueryResponse<List<PatientResponse>>> Handle(GetPatientListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}