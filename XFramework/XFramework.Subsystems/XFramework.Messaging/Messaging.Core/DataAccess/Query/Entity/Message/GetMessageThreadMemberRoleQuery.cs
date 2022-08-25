using Messaging.Domain.Generic.Contracts.Requests.Get;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Core.DataAccess.Query.Entity.Message;

public class GetMessageThreadMemberRoleQuery : GetMessageThreadMemberRoleRequest, IRequest<QueryResponse<MessageThreadMemberRoleResponse>>
{
    
}