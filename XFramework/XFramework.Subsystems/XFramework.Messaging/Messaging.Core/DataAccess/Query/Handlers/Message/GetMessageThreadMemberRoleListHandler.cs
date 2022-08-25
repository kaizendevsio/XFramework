using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Core.DataAccess.Query.Handlers.Message;

public class GetMessageThreadMemberRoleListHandler : QueryBaseHandler, IRequestHandler<GetMessageThreadMemberRoleListQuery, QueryResponse<List<MessageThreadMemberRoleResponse>>>
{
    public GetMessageThreadMemberRoleListHandler()
    {
        
    }
    public async Task<QueryResponse<List<MessageThreadMemberRoleResponse>>> Handle(GetMessageThreadMemberRoleListQuery request, CancellationToken cancellationToken)
    {
        var role = await _dataLayer.MessageThreadMemberRoles
            .Include(x => x.Role)
            .Include(x => x.MessageThreadMember)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!role.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Message Thread Member Role Found",
                IsSuccess = true
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Message Thread Member Role Found",
            IsSuccess = true,
            Response = role.Adapt<List<MessageThreadMemberRoleResponse>>()
        }; 
    }
}