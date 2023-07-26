using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Core.DataAccess.Query.Handlers.Message;

public class GetMessageThreadMemberRoleHandler : QueryBaseHandler, IRequestHandler<GetMessageThreadMemberRoleQuery, QueryResponse<MessageThreadMemberRoleResponse>>
{
    public GetMessageThreadMemberRoleHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<MessageThreadMemberRoleResponse>> Handle(GetMessageThreadMemberRoleQuery request, CancellationToken cancellationToken)
    {
        var role = await _dataLayer.MessageThreadMemberRoles
            .Include(x => x.MessageThreadMember)
            .Include(x => x.Role)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (role is null)
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
            Response = role.Adapt<MessageThreadMemberRoleResponse>()
        };
    }
}