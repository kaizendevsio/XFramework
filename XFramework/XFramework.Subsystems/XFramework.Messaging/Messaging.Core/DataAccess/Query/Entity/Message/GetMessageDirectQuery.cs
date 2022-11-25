using Messaging.Domain.Generic.Contracts.Requests.Get;
using MessageDirectResponse = Messaging.Domain.Generic.Contracts.Responses.MessageDirectResponse;

namespace Messaging.Core.DataAccess.Query.Entity.Message;

public class GetMessageDirectQuery : GetMessageDirectRequest, IRequest<QueryResponse<MessageDirectResponse>>
{
    
}