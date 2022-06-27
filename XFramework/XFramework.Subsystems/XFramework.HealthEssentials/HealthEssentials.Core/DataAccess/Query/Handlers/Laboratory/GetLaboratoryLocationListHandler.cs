using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryLocationListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryLocationListQuery, QueryResponse<List<LaboratoryLocationResponse>>>
{
    public GetLaboratoryLocationListHandler()
    {
        
    }
    public async Task<QueryResponse<List<LaboratoryLocationResponse>>> Handle(GetLaboratoryLocationListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}