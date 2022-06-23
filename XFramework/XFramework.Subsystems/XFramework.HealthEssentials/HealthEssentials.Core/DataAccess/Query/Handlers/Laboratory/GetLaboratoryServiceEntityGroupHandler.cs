using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryServiceEntityGroupHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryServiceEntityGroupQuery, QueryResponse<LaboratoryServiceEntityGroupResponse>>
{
    public GetLaboratoryServiceEntityGroupHandler()
    {
        
    }
    public async Task<QueryResponse<LaboratoryServiceEntityGroupResponse>> Handle(GetLaboratoryServiceEntityGroupQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}