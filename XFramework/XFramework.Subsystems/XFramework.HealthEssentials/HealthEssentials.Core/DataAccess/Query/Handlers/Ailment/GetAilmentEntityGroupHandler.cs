using HealthEssentials.Core.DataAccess.Query.Entity.Ailment;
using HealthEssentials.Domain.Generics.Contracts.Responses.Ailment;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Ailment;

public class GetAilmentEntityGroupHandler : QueryBaseHandler, IRequestHandler<GetAilmentEntityGroupQuery, QueryResponse<AilmentEntityGroupResponse>>
{
    public GetAilmentEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<AilmentEntityGroupResponse>> Handle(GetAilmentEntityGroupQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}