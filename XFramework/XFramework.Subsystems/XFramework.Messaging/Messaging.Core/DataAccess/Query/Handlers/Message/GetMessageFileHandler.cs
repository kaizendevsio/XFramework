using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Core.DataAccess.Query.Handlers.Message;

public class GetMessageFileHandler : QueryBaseHandler, IRequestHandler<GetMessageFileQuery, QueryResponse<MessageFileResponse>>
{
    public GetMessageFileHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<MessageFileResponse>> Handle(GetMessageFileQuery request, CancellationToken cancellationToken)
    {
        var file = await _dataLayer.MessageFiles
            .Include(x => x.Message)
            .Include(x => x.Storage)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (file is null)
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
            Response = file.Adapt<MessageFileResponse>()
        };
    }
}