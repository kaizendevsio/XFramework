using System.Security.AccessControl;
using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Core.DataAccess.Query.Handlers.Message;

public class GetMessageDirectHandler : QueryBaseHandler, IRequestHandler<GetMessageDirectQuery, QueryResponse<MessageDirectResponse>>
{
    public GetMessageDirectHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<MessageDirectResponse>> Handle(GetMessageDirectQuery request, CancellationToken cancellationToken)
    {
        var direct = await _dataLayer.MessageDirects
            .Include(x => x.ParentMessage)
            .Include(x => x.Type)
            .Include(x => x.Sender)
            .Include(x => x.Recipient)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (direct is null)
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
            Response = direct.Adapt<MessageDirectResponse>()
        };
    }
}