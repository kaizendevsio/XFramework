using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Core.DataAccess.Query.Handlers.Message;

public class GetMessageThreadMemberGroupHandler : QueryBaseHandler, IRequestHandler<GetMessageThreadMemberGroupQuery, QueryResponse<MessageThreadMemberGroupResponse>>
{
    public GetMessageThreadMemberGroupHandler()
    {
        
    }
    public async Task<QueryResponse<MessageThreadMemberGroupResponse>> Handle(GetMessageThreadMemberGroupQuery request, CancellationToken cancellationToken)
    {
        var group = await _dataLayer.MessageThreadMemberGroups
            .Include(x => x.MessageThread)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (group is null)
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
            Response = group.Adapt<MessageThreadMemberGroupResponse>()
        };
    }
}