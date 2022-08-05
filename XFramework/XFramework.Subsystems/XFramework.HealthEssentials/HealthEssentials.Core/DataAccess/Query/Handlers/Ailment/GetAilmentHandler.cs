using HealthEssentials.Core.DataAccess.Query.Entity.Ailment;
using HealthEssentials.Domain.Generics.Contracts.Responses.Ailment;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Ailment;

public class GetAilmentHandler : QueryBaseHandler, IRequestHandler<GetAilmentQuery, QueryResponse<AilmentResponse>>
{
    public GetAilmentHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<AilmentResponse>> Handle(GetAilmentQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}