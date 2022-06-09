using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryServiceEntityGroupListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryServiceEntityGroupListQuery, QueryResponse<List<LaboratoryServiceEntityGroupResponse>>>
{
    public GetLaboratoryServiceEntityGroupListHandler()
    {
        
    }
    public async Task<QueryResponse<List<LaboratoryServiceEntityGroupResponse>>> Handle(GetLaboratoryServiceEntityGroupListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}