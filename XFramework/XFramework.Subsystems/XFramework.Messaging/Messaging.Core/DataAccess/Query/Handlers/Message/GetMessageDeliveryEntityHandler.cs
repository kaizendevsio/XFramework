using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Core.DataAccess.Query.Handlers.Message;

public class GetMessageDeliveryEntityHandler : QueryBaseHandler, IRequestHandler<GetMessageDeliveryEntityQuery, QueryResponse<MessageDeliveryEntityResponse>>
{
    public GetMessageDeliveryEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<MessageDeliveryEntityResponse>> Handle(GetMessageDeliveryEntityQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.MessageDeliveryEntities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (entity is null)
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
            Message = "Data Found",
            Response = entity.Adapt<MessageDeliveryEntityResponse>()
        };
    }
}