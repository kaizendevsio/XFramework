using HealthEssentials.Domain.Generics.Contracts.Requests.MetaData.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.MetaData;

namespace HealthEssentials.Core.DataAccess.Query.Entity.MetaData;

public class GetMetaDatumListQuery : GetMetaDatumListRequest, IRequest<QueryResponse<List<MetaDatumResponse>>>
{
    
}