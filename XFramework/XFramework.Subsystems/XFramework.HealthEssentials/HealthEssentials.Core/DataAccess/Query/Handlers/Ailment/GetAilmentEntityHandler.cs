using HealthEssentials.Core.DataAccess.Query.Entity.Ailment;
using HealthEssentials.Domain.Generics.Contracts.Responses.Ailment;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Ailment;

public class GetAilmentEntityHandler : QueryBaseHandler, IRequestHandler<GetAilmentEntityQuery, QueryResponse<AilmentEntityResponse>>
{
    public GetAilmentEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<AilmentEntityResponse>> Handle(GetAilmentEntityQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}