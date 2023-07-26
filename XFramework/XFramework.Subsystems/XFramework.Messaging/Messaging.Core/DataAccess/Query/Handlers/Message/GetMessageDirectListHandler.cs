using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Core.DataAccess.Query.Handlers.Message;

public class GetMessageDirectListHandler : QueryBaseHandler, IRequestHandler<GetMessageDirectListQuery, QueryResponse<List<MessageDirectResponse>>>
{
    public GetMessageDirectListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<MessageDirectResponse>>> Handle(GetMessageDirectListQuery request, CancellationToken cancellationToken)
    {
        var direct = await _dataLayer.MessageDirects
            .Include(x => x.ParentMessage)
            .Include(x => x.Type)
            .Include(x => x.Sender)
            .Include(x => x.Recipient)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!direct.Any())
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
            Message = "Data found",
            Response = direct.Adapt<List<MessageDirectResponse>>()
        }; 

    }
}