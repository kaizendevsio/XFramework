using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Responses;
using TypeSupport.Extensions;

namespace Messaging.Core.DataAccess.Query.Handlers.Message;

public class GetMessageTypeHandler : QueryBaseHandler, IRequestHandler<GetMessageTypeQuery, QueryResponse<MessageTypeResponse>>
{
    public GetMessageTypeHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<MessageTypeResponse>> Handle(GetMessageTypeQuery request, CancellationToken cancellationToken)
    {
        var type = await _dataLayer.MessageTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (type is null)
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
            Response = type.Adapt<MessageTypeResponse>()
        };

    }
}