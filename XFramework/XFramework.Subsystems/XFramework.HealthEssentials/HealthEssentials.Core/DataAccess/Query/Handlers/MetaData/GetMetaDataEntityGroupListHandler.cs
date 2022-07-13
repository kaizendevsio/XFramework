using HealthEssentials.Core.DataAccess.Query.Entity.MetaData;
using HealthEssentials.Domain.Generics.Contracts.Responses.MetaData;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.MetaData;

public class GetMetaDataEntityGroupListHandler : QueryBaseHandler, IRequestHandler<GetMetaDataEntityGroupListQuery, QueryResponse<List<MetaDataEntityGroupResponse>>>
{
    public GetMetaDataEntityGroupListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<MetaDataEntityGroupResponse>>> Handle(GetMetaDataEntityGroupListQuery request, CancellationToken cancellationToken)
    {
        var group = await _dataLayer.HealthEssentialsContext.MetaDataEntityGroups
            .Where(x => EF.Functions.Like(x.Name, $"{request.SearchField}"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!group.Any())
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
            Response = group.Adapt<List<MetaDataEntityGroupResponse>>()
        };

    }
}