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
        var metaDatum = await _dataLayer.HealthEssentialsContext.MetaData
            .Include(x => x.Type)
            .ThenInclude(x => x.Group)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (metaDatum is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No record found",
                
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Record found",
            Response = metaDatum.Adapt<MetaDatumResponse>()
        };
    }
}