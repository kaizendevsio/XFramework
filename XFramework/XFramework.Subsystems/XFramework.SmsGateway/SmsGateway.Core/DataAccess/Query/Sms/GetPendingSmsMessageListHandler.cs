using SmsGateway.Domain.Generic.Contracts.Requests.Get;
using SmsGateway.Domain.Generic.Contracts.Responses.Sms;

namespace SmsGateway.Core.DataAccess.Query.Handlers.Sms;

public class GetPendingSmsMessageListHandler : IRequestHandler<GetPendingSmsMessageListRequest, QueryResponse<List<MessageDirectResponse>>>
{
    private readonly ICachingService _cachingService;

    public GetPendingSmsMessageListHandler(ICachingService cachingService)
    {
        _cachingService = cachingService;
    }
    
    public async Task<QueryResponse<List<MessageDirectResponse>>> Handle(GetPendingSmsMessageListRequest request, CancellationToken cancellationToken)
    {
        var messageDirectResponses = _cachingService.PendingMessageList.ToList();

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            
            Response = messageDirectResponses
        };
    }
}