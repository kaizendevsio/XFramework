using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Core.DataAccess.Query.Handlers.Message;

public class GetMessageThreadMemberHandler : QueryBaseHandler, IRequestHandler<GetMessageThreadMemberQuery, QueryResponse<MessageThreadMemberResponse>>
{
    public GetMessageThreadMemberHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<QueryResponse<MessageThreadMemberResponse>> Handle(GetMessageThreadMemberQuery request, CancellationToken cancellationToken)
    {
        var member = await _dataLayer.MessageThreadMembers
            .Include(x => x.MessageThread)
            .Include(x => x.Group)
            .Include(x => x.IdentityCredential)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (member is null)
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
            Response = member.Adapt<MessageThreadMemberResponse>()
        };
    }
}