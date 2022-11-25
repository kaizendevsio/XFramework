using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Core.DataAccess.Query.Handlers.Message;

public class GetMessageReactionHandler : QueryBaseHandler, IRequestHandler<GetMessageReactionQuery, QueryResponse<MessageReactionResponse>>
{
    public GetMessageReactionHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<MessageReactionResponse>> Handle(GetMessageReactionQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}