using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Core.DataAccess.Query.Handlers.Message;

public class GetMessageThreadMemberGroupListHandler : QueryBaseHandler, IRequestHandler<GetMessageThreadMemberGroupListQuery, QueryResponse<List<MessageThreadMemberGroupResponse>>>
{
    public GetMessageThreadMemberGroupListHandler()
    {
        
    }
    public async Task<QueryResponse<List<MessageThreadMemberGroupResponse>>> Handle(GetMessageThreadMemberGroupListQuery request, CancellationToken cancellationToken)
    {
        var group = await _dataLayer.MessageThreadMemberGroups
            .Include(x => x.MessageThread)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!group.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Message Thread Member Groups Found",
                IsSuccess = true
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Message Thread Member Groups Found",
            IsSuccess = true,
            Response = group.Adapt<List<MessageThreadMemberGroupResponse>>()
        }; 
    }
}