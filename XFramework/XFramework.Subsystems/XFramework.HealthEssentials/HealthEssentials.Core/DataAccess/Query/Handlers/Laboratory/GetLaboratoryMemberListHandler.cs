using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryMemberListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryMemberListQuery, QueryResponse<List<LaboratoryMemberResponse>>>
{
    public GetLaboratoryMemberListHandler()
    {
        
    }
    public async Task<QueryResponse<List<LaboratoryMemberResponse>>> Handle(GetLaboratoryMemberListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}