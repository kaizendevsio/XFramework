using IdentityServer.Core.DataAccess.Query.Entity.Application;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Application;

public class GetAllAppInfoQueryHandler : QueryBaseHandler, IRequestHandler<GetAppAppListQuery, QueryResponseBO<List<GetApplicationListResponse>>>
{
    public GetAllAppInfoQueryHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<QueryResponseBO<List<GetApplicationListResponse>>> Handle(GetAppAppListQuery request, CancellationToken cancellationToken)
    {
        var result = await _dataLayer.TblApplications.ToListAsync(cancellationToken: cancellationToken);
        if (!result.Any())
        {
            return new QueryResponseBO<List<GetApplicationListResponse>>()
            {
                Message = $"No applications exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var r = result.Adapt<List<GetApplicationListResponse>>();
        return new QueryResponseBO<List<GetApplicationListResponse>>()
        {
            Response = r
        };
    }
}