using SmsGateway.Core.DataAccess.Query.Entity.Sms;
using SmsGateway.Domain.Generic.Contracts.Responses.Sms;

namespace SmsGateway.Core.DataAccess.Query.Handlers.Sms;

public class GetScheduledSmsMessageListHandler : QueryBaseHandler, IRequestHandler<GetScheduledSmsMessageListQuery, QueryResponse<List<MessageDirectResponse>>>
{

    public GetScheduledSmsMessageListHandler(ICachingService cachingService)
    {
        _cachingService = cachingService;
    }
    
    public async Task<QueryResponse<List<MessageDirectResponse>>> Handle(GetScheduledSmsMessageListQuery request, CancellationToken cancellationToken)
    {
        var messageDirectResponses = _cachingService.ScheduledMessageList.ToList();

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            
            Response = messageDirectResponses
        };
    }
}