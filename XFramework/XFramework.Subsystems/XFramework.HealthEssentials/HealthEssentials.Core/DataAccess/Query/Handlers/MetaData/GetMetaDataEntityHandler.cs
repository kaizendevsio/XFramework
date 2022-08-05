using HealthEssentials.Core.DataAccess.Query.Entity.MetaData;
using HealthEssentials.Domain.Generics.Contracts.Responses.MetaData;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.MetaData;

public class GetMetaDataEntityHandler : QueryBaseHandler, IRequestHandler<GetMetaDataEntityQuery, QueryResponse<MetaDataEntityResponse>>
{
    public GetMetaDataEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<MetaDataEntityResponse>> Handle(GetMetaDataEntityQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}