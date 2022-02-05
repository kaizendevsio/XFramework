using Wallets.Core.DataAccess.Query.Entity.Wallets;

namespace Wallets.Core.DataAccess.Query.Handlers.Wallets;

public class GetWalletEntityHandler : QueryBaseHandler, IRequestHandler<GetWalletEntityQuery, QueryResponseBO<WalletEntityResponse>>
{
    public GetWalletEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponseBO<WalletEntityResponse>> Handle(GetWalletEntityQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.TblWalletEntities.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken: cancellationToken);
        if (entity == null)
        {
            return new QueryResponseBO<WalletEntityResponse>()
            {
                Message = $"Wallet entity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        return new QueryResponseBO<WalletEntityResponse>()
        {
            Response = entity.Adapt<WalletEntityResponse>()
        };
    }
}