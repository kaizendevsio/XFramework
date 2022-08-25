using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Core.DataAccess.Query.Handlers.Message;

public class GetMessageThreadListHandler : QueryBaseHandler, IRequestHandler<GetMessageThreadListQuery, QueryResponse<List<MessageThreadResponse>>>
{
    public GetMessageThreadListHandler()
    {
        
    }
    public async Task<QueryResponse<List<MessageThreadResponse>>> Handle(GetMessageThreadListQuery request, CancellationToken cancellationToken)
    {
        var thread = await _dataLayer.MessageThreads
            .Include(x => x.Entity)
            .Where(x => EF.Functions.ILike(x.Name, $"%{request.SearchField}%"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!thread.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Message Threads Found",
                IsSuccess = true
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Message Threads Found",
            IsSuccess = true,
            Response = thread.Adapt<List<MessageThreadResponse>>()
        }; 
    }
}