using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryServiceEntityListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryServiceEntityListQuery, QueryResponse<List<LaboratoryServiceEntityResponse>>>
{
    public GetLaboratoryServiceEntityListHandler()
    {
        
    }
    public async Task<QueryResponse<List<LaboratoryServiceEntityResponse>>> Handle(GetLaboratoryServiceEntityListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}