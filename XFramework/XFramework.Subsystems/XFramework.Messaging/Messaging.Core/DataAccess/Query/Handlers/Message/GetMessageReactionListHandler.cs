using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Core.DataAccess.Query.Handlers.Message;

public class GetMessageReactionListHandler : QueryBaseHandler, IRequestHandler<GetMessageReactionListQuery, QueryResponse<List<MessageReactionResponse>>>
{
    public GetMessageReactionListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<MessageReactionResponse>>> Handle(GetMessageReactionListQuery request, CancellationToken cancellationToken)
    {
        var reaction = await _dataLayer.MessageReactions
            .Include(x => x.Entity)
            .Include(x => x.Message)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!reaction.Any())
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
            Response = reaction.Adapt<List<MessageReactionResponse>>()
        }; 

    }
}