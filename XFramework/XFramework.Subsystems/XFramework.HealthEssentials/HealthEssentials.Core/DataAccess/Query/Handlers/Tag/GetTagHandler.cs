using HealthEssentials.Core.DataAccess.Query.Entity.Tag;
using HealthEssentials.Domain.Generics.Contracts.Responses.Tag;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Tag;

public class GetTagHandler : QueryBaseHandler, IRequestHandler<GetTagQuery, QueryResponse<TagResponse>>
{
    public GetTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<TagResponse>> Handle(GetTagQuery request, CancellationToken cancellationToken)
    {
        var tag = await _dataLayer.HealthEssentialsContext.Tags
            .Include(x => x.Type)
            .ThenInclude(x => x.Group)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (tag is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "Tag not found",
                
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Tag found",
            Response = tag.Adapt<TagResponse>()
        };
    }
}