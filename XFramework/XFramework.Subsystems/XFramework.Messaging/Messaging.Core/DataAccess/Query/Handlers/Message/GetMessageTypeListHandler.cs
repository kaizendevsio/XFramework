using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Core.DataAccess.Query.Handlers.Message;

public class GetMessageTypeListHandler : QueryBaseHandler, IRequestHandler<GetMessageTypeListQuery, QueryResponse<List<MessageTypeResponse>>>
{
    public GetMessageTypeListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<MessageTypeResponse>>> Handle(GetMessageTypeListQuery request, CancellationToken cancellationToken)
    {
        var type = await _dataLayer.MessageTypes
            .Where(x => EF.Functions.ILike(x.Name, $"%{request.SearchField}%"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!type.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No data Found",
                
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Data Found",
            Response = type.Adapt<List<MessageTypeResponse>>()
        }; 
    }
}