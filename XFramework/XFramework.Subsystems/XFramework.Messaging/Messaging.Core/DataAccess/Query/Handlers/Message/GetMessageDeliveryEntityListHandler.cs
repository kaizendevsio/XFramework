using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Core.DataAccess.Query.Handlers.Message;

public class GetMessageDeliveryEntityListHandler : QueryBaseHandler, IRequestHandler<GetMessageDeliveryEntityListQuery, QueryResponse<List<MessageDeliveryEntityResponse>>>
{
    public GetMessageDeliveryEntityListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<MessageDeliveryEntityResponse>>> Handle(GetMessageDeliveryEntityListQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.MessageDeliveryEntities
            .Where(x => EF.Functions.ILike(x.Name, $"%{request.SearchField}%"))
            .OrderBy(x => x.Name)
            .Take(request.PageIndex)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!entity.Any())
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
            Response = entity.Adapt<List<MessageDeliveryEntityResponse>>()
        }; 

    }
}