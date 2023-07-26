using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Core.DataAccess.Query.Handlers.Message;

public class GetMessageThreadMemberListHandler : QueryBaseHandler, IRequestHandler<GetMessageThreadMemberListQuery, QueryResponse<List<MessageThreadMemberResponse>>>
{
    public GetMessageThreadMemberListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<QueryResponse<List<MessageThreadMemberResponse>>> Handle(GetMessageThreadMemberListQuery request, CancellationToken cancellationToken)
    {
        var member = await _dataLayer.MessageThreadMembers
            .Include(x => x.MessageThread)
            .Include(x => x.Group)
            .Include(x => x.IdentityCredential)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!member.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Message Thread Member Found",
                
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Message Thread Member Found",
            Response = member.Adapt<List<MessageThreadMemberResponse>>()
        }; 
    }
}