using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Responses;
using TypeSupport.Extensions;

namespace Messaging.Core.DataAccess.Query.Handlers.Message;

public class GetMessageThreadEntityHandler : QueryBaseHandler, IRequestHandler<GetMessageThreadEntityQuery, QueryResponse<MessageThreadEntityResponse>>
{
    public GetMessageThreadEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<MessageThreadEntityResponse>> Handle(GetMessageThreadEntityQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.MessageThreadEntities
            .Include(x => x.MessageType)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (entity is null)
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
            Response = entity.Adapt<MessageThreadEntityResponse>()
        };
    }
}