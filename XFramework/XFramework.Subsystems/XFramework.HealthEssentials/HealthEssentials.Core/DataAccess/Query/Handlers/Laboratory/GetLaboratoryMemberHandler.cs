using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryMemberHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryMemberQuery, QueryResponse<LaboratoryMemberResponse>>
{
    public GetLaboratoryMemberHandler()
    {
        
    }
    public async Task<QueryResponse<LaboratoryMemberResponse>> Handle(GetLaboratoryMemberQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}