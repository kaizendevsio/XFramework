using SmsGateway.Core.DataAccess.Query.Entity.Sms;
using SmsGateway.Domain.Generic.Contracts.Responses.Sms;

namespace SmsGateway.Core.DataAccess.Query.Handlers.Sms;

public class GetPendingSmsMessageListHandler : QueryBaseHandler, IRequestHandler<GetPendingSmsMessageListQuery, QueryResponse<List<MessageDirectResponse>>>
{

    public GetPendingSmsMessageListHandler(ICachingService cachingService)
    {
        _cachingService = cachingService;
    }
    
    public async Task<QueryResponse<List<MessageDirectResponse>>> Handle(GetPendingSmsMessageListQuery request, CancellationToken cancellationToken)
    {
        var messageDirectResponses = _cachingService.PendingMessageList.ToList();

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            
            Response = messageDirectResponses
        };
    }
}