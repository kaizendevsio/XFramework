using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryServiceEntityHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryServiceEntityQuery, QueryResponse<LaboratoryServiceEntityResponse>>
{
    public GetLaboratoryServiceEntityHandler()
    {
        
    }
    public async Task<QueryResponse<LaboratoryServiceEntityResponse>> Handle(GetLaboratoryServiceEntityQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}