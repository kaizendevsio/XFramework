using HealthEssentials.Core.DataAccess.Query.Entity.MetaData;
using HealthEssentials.Domain.Generics.Contracts.Responses.MetaData;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.MetaData;

public class GetMetaDatumHandler : QueryBaseHandler, IRequestHandler<GetMetaDatumQuery, QueryResponse<MetaDatumResponse>>
{
    public GetMetaDatumHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<MetaDatumResponse>> Handle(GetMetaDatumQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}