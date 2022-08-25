using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Core.DataAccess.Query.Handlers.Message;

public class GetMessageHandler : QueryBaseHandler, IRequestHandler<GetMessageQuery, QueryResponse<MessageResponse>>
{
    public GetMessageHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<QueryResponse<MessageResponse>> Handle(GetMessageQuery request, CancellationToken cancellationToken)
    {
        var message = await _dataLayer.Messages
            .Include(x => x.MessageThread)
            .Include(x => x.MessageThreadMember)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (message is null)
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
            Message = "Data Found",
            Response = message.Adapt<MessageResponse>()
        };
    }
}