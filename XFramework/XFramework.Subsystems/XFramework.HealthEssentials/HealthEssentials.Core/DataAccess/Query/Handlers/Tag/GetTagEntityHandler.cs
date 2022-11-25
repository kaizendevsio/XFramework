using HealthEssentials.Core.DataAccess.Query.Entity.Tag;
using HealthEssentials.Domain.Generics.Contracts.Responses.Tag;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Tag;

public class GetTagEntityHandler : QueryBaseHandler, IRequestHandler<GetTagEntityQuery, QueryResponse<TagEntityResponse>>
{
    public GetTagEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<TagEntityResponse>> Handle(GetTagEntityQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.HealthEssentialsContext.TagEntities
            .Include(x => x.Group)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (entity is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No entity found",
                IsSuccess = true
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Entity found",
            Response = entity.Adapt<TagEntityResponse>()
        };
    }
}