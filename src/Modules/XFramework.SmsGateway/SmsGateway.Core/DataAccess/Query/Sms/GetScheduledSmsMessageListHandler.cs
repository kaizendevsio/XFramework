using SmsGateway.Domain.Shared.Contracts.Requests.Get;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;

namespace SmsGateway.Core.DataAccess.Query.Handlers.Sms;

public class GetScheduledSmsMessageListHandler : IRequestHandler<GetScheduledSmsMessageListRequest, QueryResponse<List<MessageDirectResponse>>>
{
    private readonly ICachingService _cachingService;

    public GetScheduledSmsMessageListHandler(ICachingService cachingService)
    {
        _cachingService = cachingService;
    }
    
    public async Task<QueryResponse<List<MessageDirectResponse>>> Handle(GetScheduledSmsMessageListRequest request, CancellationToken cancellationToken)
    {
        var messageDirectResponses = _cachingService.ScheduledMessageList.ToList();

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            
            Response = messageDirectResponses
        };
    }
}