using HealthEssentials.Core.DataAccess.Query.Entity.MetaData;
using HealthEssentials.Domain.Generics.Contracts.Responses.MetaData;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.MetaData;

public class GetMetaDataEntityGroupHandler : QueryBaseHandler, IRequestHandler<GetMetaDataEntityGroupQuery, QueryResponse<MetaDataEntityGroupResponse>>
{
    public GetMetaDataEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<MetaDataEntityGroupResponse>> Handle(GetMetaDataEntityGroupQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}