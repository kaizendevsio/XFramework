using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Core.DataAccess.Query.Handlers.Message;

public class GetMessageThreadEntityListHandler : QueryBaseHandler, IRequestHandler<GetMessageThreadEntityListQuery, QueryResponse<List<MessageThreadEntityResponse>>>
{
    public GetMessageThreadEntityListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<MessageThreadEntityResponse>>> Handle(GetMessageThreadEntityListQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.MessageThreadEntities
            .Include(x => x.MessageType)
            .Where(x => EF.Functions.ILike(x.Name, $"%{request.SearchField}%"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
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
            Response = entity.Adapt<List<MessageThreadEntityResponse>>()
        }; 

    }
}