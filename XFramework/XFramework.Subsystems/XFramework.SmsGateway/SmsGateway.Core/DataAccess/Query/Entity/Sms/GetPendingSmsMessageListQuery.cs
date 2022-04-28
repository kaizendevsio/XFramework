using SmsGateway.Domain.Generic.Contracts.Requests.Get;
using SmsGateway.Domain.Generic.Contracts.Responses.Sms;

namespace SmsGateway.Core.DataAccess.Query.Entity.Sms;

public class GetPendingSmsMessageListQuery : GetPendingSmsMessageListRequest, IRequest<QueryResponse<List<MessageDirectResponse>>>
{
    
}