using Messaging.Domain.Generic.Contracts.Requests.Get;
using MessageDirectResponse = Messaging.Domain.Generic.Contracts.Responses.MessageDirectResponse;

namespace Messaging.Core.DataAccess.Query.Entity.Message;

public class GetMessageDirectListQuery : GetMessageDirectListRequest, IRequest<QueryResponse<List<MessageDirectResponse>>>
{
    
}