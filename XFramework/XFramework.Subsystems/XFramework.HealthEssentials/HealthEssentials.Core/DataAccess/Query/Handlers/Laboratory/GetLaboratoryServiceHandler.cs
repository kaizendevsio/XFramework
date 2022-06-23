using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryServiceHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryServiceQuery, QueryResponse<LaboratoryServiceResponse>>
{
    public GetLaboratoryServiceHandler()
    {
        
    }
    public async Task<QueryResponse<LaboratoryServiceResponse>> Handle(GetLaboratoryServiceQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}