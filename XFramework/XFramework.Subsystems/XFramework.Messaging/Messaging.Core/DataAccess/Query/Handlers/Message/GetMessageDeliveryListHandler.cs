using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Core.DataAccess.Query.Handlers.Message;

public class GetMessageDeliveryListHandler : QueryBaseHandler, IRequestHandler<GetMessageDeliveryListQuery, QueryResponse<List<MessageDeliveryResponse>>>
{
    public GetMessageDeliveryListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<MessageDeliveryResponse>>> Handle(GetMessageDeliveryListQuery request, CancellationToken cancellationToken)
    {
        var delivery = await _dataLayer.MessageDeliveries
            .Include(x => x.MessageThreadMember)
            .Include(x => x.Message)
            .Include(x => x.Entity)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!delivery.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No data found",
                IsSuccess = true
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Data found",
            IsSuccess = true,
            Response = delivery.Adapt<List<MessageDeliveryResponse>>()
        }; 

    }
}