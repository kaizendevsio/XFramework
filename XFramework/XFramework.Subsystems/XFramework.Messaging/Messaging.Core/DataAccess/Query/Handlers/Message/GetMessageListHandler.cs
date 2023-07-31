using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Core.DataAccess.Query.Handlers.Message;

public class GetMessageListHandler : QueryBaseHandler, IRequestHandler<GetMessageListQuery, QueryResponse<List<MessageResponse>>>
{
    public GetMessageListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<QueryResponse<List<MessageResponse>>> Handle(GetMessageListQuery request, CancellationToken cancellationToken)
    {
        var message = await _dataLayer.Messages
            .Include(x => x.MessageThread)
            .Include(x => x.MessageThreadMember)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!message.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No messages found",
                
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Messages found",
            Response = message.Adapt<List<MessageResponse>>()
        }; 
    }
}