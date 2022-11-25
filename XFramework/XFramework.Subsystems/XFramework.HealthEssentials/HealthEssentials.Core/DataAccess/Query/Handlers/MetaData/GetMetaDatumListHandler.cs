using HealthEssentials.Core.DataAccess.Query.Entity.MetaData;
using HealthEssentials.Domain.Generics.Contracts.Responses.MetaData;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.MetaData;

public class GetMetaDatumListHandler : QueryBaseHandler, IRequestHandler<GetMetaDatumListQuery, QueryResponse<List<MetaDatumResponse>>>
{
    public GetMetaDatumListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<MetaDatumResponse>>> Handle(GetMetaDatumListQuery request, CancellationToken cancellationToken)
    {
        var metaDatum = await _dataLayer.HealthEssentialsContext.MetaData
            .Include(x => x.Entity)
            .ThenInclude(x => x.Group)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!metaDatum.Any())
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
            Response = metaDatum.Adapt<List<MetaDatumResponse>>()
        };
    }
}