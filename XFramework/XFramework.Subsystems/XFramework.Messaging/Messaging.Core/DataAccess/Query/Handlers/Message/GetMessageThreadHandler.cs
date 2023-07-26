using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Core.DataAccess.Query.Handlers.Message;

public class GetMessageThreadHandler : QueryBaseHandler, IRequestHandler<GetMessageThreadQuery, QueryResponse<MessageThreadResponse>>
{
    public GetMessageThreadHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<QueryResponse<MessageThreadResponse>> Handle(GetMessageThreadQuery request, CancellationToken cancellationToken)
    {
        var thread = await _dataLayer.MessageThreads
            .Include(x => x.Entity)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (thread is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No data found",
                
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Data Found",
            Response = thread.Adapt<MessageThreadResponse>()
        };
    }
}