using Wallets.Core.DataAccess.Query.Entity.Wallets.Identity;

namespace Wallets.Core.DataAccess.Query.Handlers.Wallets.Identity;

public class GetWalletHandler : QueryBaseHandler, IRequestHandler<GetWalletQuery, QueryResponse<WalletResponse>>
{
    public GetWalletHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
        
    public async Task<QueryResponse<WalletResponse>> Handle(GetWalletQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.Wallets
            .Include(i => i.WalletEntity)
            .Include(i => i.WalletTransactionSourceUserWallets)
            .Include(i => i.WalletTransactionTargetUserWallets)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken);
        if (entity == null)
        {
            return new ()
            {
                Message = $" Wallet with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        return new ()
        {
            Response = entity.Adapt<WalletResponse>()
        };
    }
        
}