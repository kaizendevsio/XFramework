using Wallets.Core.DataAccess.Query.Entity.Wallets;

namespace Wallets.Core.DataAccess.Query.Handlers.Wallets;

public class GetWalletEntityListHandler : QueryBaseHandler, IRequestHandler<GetWalletEntityListQuery, QueryResponseBO<List<WalletEntityResponse>>>
{
    public GetWalletEntityListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
        
    public async Task<QueryResponseBO<List<WalletEntityResponse>>> Handle(GetWalletEntityListQuery request, CancellationToken cancellationToken)
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
            return new QueryResponseBO<List<WalletEntityResponse>>()
            {
                Message = $"No identity exists",
                HttpStatusCode = HttpStatusCode.NoContent
            };
        }

        var r = result.Adapt<List<WalletEntityResponse>>();
        return new QueryResponseBO<List<WalletEntityResponse>>()
        {
            Response = r
        };
    }
}