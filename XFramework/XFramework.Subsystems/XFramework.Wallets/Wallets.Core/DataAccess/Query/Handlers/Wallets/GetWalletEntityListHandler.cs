using Wallets.Core.DataAccess.Query.Entity.Wallets;

namespace Wallets.Core.DataAccess.Query.Handlers.Wallets;

public class GetWalletEntityListHandler : QueryBaseHandler, IRequestHandler<GetWalletEntityListQuery, QueryResponse<List<WalletEntityResponse>>>
{
    public GetWalletEntityListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
        
    public async Task<QueryResponse<List<WalletEntityResponse>>> Handle(GetWalletEntityListQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.TblApplications.FirstOrDefaultAsync(i => i.Guid == $"{request.ApplicationGuid}", cancellationToken);
        if (entity == null)
        {
            return new ()
            {
                Message = $"Identity with Guid {request.ApplicationGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var result = await _dataLayer.TblWalletEntities
            .Take(1000)
            .AsNoTracking()
            .Where(i => i.ApplicationId == entity.Id)
            .ToListAsync(cancellationToken: cancellationToken);
        if (!result.Any())
        {
            return new QueryResponse<List<WalletEntityResponse>>()
            {
                Message = $"No identity exists",
                HttpStatusCode = HttpStatusCode.NoContent
            };
        }

        var r = result.Adapt<List<WalletEntityResponse>>();
        return new QueryResponse<List<WalletEntityResponse>>()
        {
            Response = r
        };
    }
}