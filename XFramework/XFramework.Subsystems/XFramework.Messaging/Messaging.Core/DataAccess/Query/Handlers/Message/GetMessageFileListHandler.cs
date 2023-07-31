using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Core.DataAccess.Query.Handlers.Message;

public class GetMessageFileListHandler : QueryBaseHandler, IRequestHandler<GetMessageFileListQuery, QueryResponse<List<MessageFileResponse>>>
{
    public GetMessageFileListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<MessageFileResponse>>> Handle(GetMessageFileListQuery request, CancellationToken cancellationToken)
    {
        var file = await _dataLayer.MessageFiles
            .Include(x => x.Message)
            .Include(x => x.Storage)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!file.Any())
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
            Response = file.Adapt<List<MessageFileResponse>>()
        }; 

    }
}