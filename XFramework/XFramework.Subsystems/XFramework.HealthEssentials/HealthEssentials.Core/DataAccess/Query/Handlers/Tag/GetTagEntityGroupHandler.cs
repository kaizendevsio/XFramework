using HealthEssentials.Core.DataAccess.Query.Entity.Tag;
using HealthEssentials.Domain.Generics.Contracts.Responses.Tag;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Tag;

public class GetTagEntityGroupHandler : QueryBaseHandler, IRequestHandler<GetTagEntityGroupQuery, QueryResponse<TagEntityGroupResponse>>
{
    public GetTagEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<TagEntityGroupResponse>> Handle(GetTagEntityGroupQuery request, CancellationToken cancellationToken)
    {
        var group = await _dataLayer.HealthEssentialsContext.TagEntityGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (group is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No group found",
                IsSuccess = true
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Group found",
            Response = group.Adapt<TagEntityGroupResponse>()
        };
    }
}