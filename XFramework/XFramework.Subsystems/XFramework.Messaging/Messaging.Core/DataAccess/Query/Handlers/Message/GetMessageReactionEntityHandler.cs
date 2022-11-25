using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Core.DataAccess.Query.Handlers.Message;

public class GetMessageReactionEntityHandler : QueryBaseHandler, IRequestHandler<GetMessageReactionEntityQuery, QueryResponse<MessageReactionEntityResponse>>
{
    public GetMessageReactionEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<MessageReactionEntityResponse>> Handle(GetMessageReactionEntityQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}