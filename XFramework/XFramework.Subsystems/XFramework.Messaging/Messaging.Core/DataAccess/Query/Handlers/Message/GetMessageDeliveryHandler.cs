using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Core.DataAccess.Query.Handlers.Message;

public class GetMessageDeliveryHandler : QueryBaseHandler, IRequestHandler<GetMessageDeliveryQuery, QueryResponse<MessageDeliveryResponse>>
{
    public GetMessageDeliveryHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<MessageDeliveryResponse>> Handle(GetMessageDeliveryQuery request, CancellationToken cancellationToken)
    {
        var delivery = await _dataLayer.MessageDeliveries
            .Include(x => x.MessageThreadMember)
            .Include(x => x.Message)
            .Include(x => x.Entity)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (delivery is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No data found",
                
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Data Found",
            Response = delivery.Adapt<MessageDeliveryResponse>()
        };
    }
}