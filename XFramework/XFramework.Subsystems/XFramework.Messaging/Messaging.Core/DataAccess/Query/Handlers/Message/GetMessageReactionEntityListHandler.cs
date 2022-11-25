using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Core.DataAccess.Query.Handlers.Message;

public class GetMessageReactionEntityListHandler : QueryBaseHandler, IRequestHandler<GetMessageReactionEntityListQuery, QueryResponse<List<MessageReactionEntityResponse>>>
{
    public GetMessageReactionEntityListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<MessageReactionEntityResponse>>> Handle(GetMessageReactionEntityListQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.MessageReactionEntities
            .OrderBy(x => x.CreatedAt)
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
            Message = "Data found",
            IsSuccess = true,
            Response = entity.Adapt<List<MessageReactionEntityResponse>>()
        }; 

    }
}