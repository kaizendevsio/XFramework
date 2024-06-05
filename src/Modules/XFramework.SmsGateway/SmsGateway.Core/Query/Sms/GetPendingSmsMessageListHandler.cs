using SmsGateway.Domain.Shared.Contracts.Requests.Get;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;

namespace SmsGateway.Core.Query.Sms;

public class GetPendingSmsMessageListHandler : IRequestHandler<GetPendingSmsMessageListRequest, QueryResponse<List<SmsNodeJob>>>
{
    private readonly ICachingService _cachingService;

    public GetPendingSmsMessageListHandler(ICachingService cachingService)
    {
        _cachingService = cachingService;
    }
    
    public async Task<QueryResponse<List<SmsNodeJob>>> Handle(GetPendingSmsMessageListRequest request, CancellationToken cancellationToken)
    {
        var messageDirectResponses = _cachingService.PendingMessageList
            .Where(x => x.Value.AgentClusterId == request.AgentClusterId)
            .Select(x => x.Value)
            .ToList();

        return new()
        {
            HttpStatusCode = HttpStatusCode.OK,
            Response = messageDirectResponses
        };
    }
}