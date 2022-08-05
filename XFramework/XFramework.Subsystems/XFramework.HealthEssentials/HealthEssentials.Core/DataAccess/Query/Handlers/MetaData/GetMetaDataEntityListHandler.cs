using HealthEssentials.Core.DataAccess.Query.Entity.MetaData;
using HealthEssentials.Domain.Generics.Contracts.Responses.MetaData;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.MetaData;

public class GetMetaDataEntityListHandler : QueryBaseHandler, IRequestHandler<GetMetaDataEntityListQuery, QueryResponse<List<MetaDataEntityResponse>>>
{
    public GetMetaDataEntityListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<MetaDataEntityResponse>>> Handle(GetMetaDataEntityListQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.HealthEssentialsContext.MetaDataEntities
            .Include(x => x.Group)
            .Where(x => EF.Functions.ILike(x.Name, $"%{request.SearchField}%"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!entity.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No data found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Success",
            IsSuccess = true,
            Response = entity.Adapt<List<MetaDataEntityResponse>>()
        };
    }
}