using Wallets.Core.DataAccess.Query.Entity.Wallets;

namespace Wallets.Core.DataAccess.Query.Handlers.Wallets;

public class GetWalletEntityHandler : QueryBaseHandler, IRequestHandler<GetWalletEntityQuery, QueryResponse<WalletEntityResponse>>
{
    public GetWalletEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<WalletEntityResponse>> Handle(GetWalletEntityQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.TblWalletEntities.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken: cancellationToken);
        if (entity == null)
        {
            return new QueryResponse<WalletEntityResponse>()
            {
                Message = $"Wallet entity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        return new QueryResponse<WalletEntityResponse>()
        {
            Response = entity.Adapt<WalletEntityResponse>()
        };
    }
}