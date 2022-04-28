using SmsGateway.Domain.Generic.Contracts.Requests.Get;
using SmsGateway.Domain.Generic.Contracts.Responses.Sms;

namespace SmsGateway.Core.DataAccess.Query.Entity.Sms;

public class GetScheduledSmsMessageListQuery : GetScheduledSmsMessageListRequest, IRequest<QueryResponse<List<MessageDirectResponse>>>
{
    
}   