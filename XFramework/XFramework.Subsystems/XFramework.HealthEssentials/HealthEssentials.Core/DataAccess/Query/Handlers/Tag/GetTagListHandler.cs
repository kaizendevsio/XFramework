using HealthEssentials.Core.DataAccess.Query.Entity.Tag;
using HealthEssentials.Domain.Generics.Contracts.Responses.Tag;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Tag;

public class GetTagListHandler : QueryBaseHandler, IRequestHandler<GetTagListQuery, QueryResponse<List<TagResponse>>>
{
    public GetTagListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<TagResponse>>> Handle(GetTagListQuery request, CancellationToken cancellationToken)
    {
        var tag = await _dataLayer.HealthEssentialsContext.Tags
            .Include(x => x.Entity)
            .ThenInclude(x => x.Group)
            .Where(x => EF.Functions.ILike(x.Name, $"%{request.SearchField}%"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!tag.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No tag found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Tag found",
            IsSuccess = true,
            Response = tag.Adapt<List<TagResponse>>()
        };
    }
}