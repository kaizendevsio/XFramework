using Wallets.Core.DataAccess.Query.Entity.Wallets.Identity;

namespace Wallets.Core.DataAccess.Query.Handlers.Wallets.Identity;

public class GetWalletListHandler : QueryBaseHandler, IRequestHandler<GetWalletListQuery, QueryResponseBO<List<WalletResponse>>>
{
    private readonly IDataLayer _dataLayer;

    public GetWalletListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
        
    public async Task<QueryResponseBO<List<WalletResponse>>> Handle(GetWalletListQuery request, CancellationToken cancellationToken)
    {
        var credential = await _dataLayer.TblIdentityCredentials
            .Include(i => i.IdentityInfo)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.CredentialGuid}", cancellationToken);
            
        if (credential == null)
        {
            return new()
            {
                Message = $"Credentials does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

                    
        var result = await _dataLayer.TblUserWallets
            .Where(i => i.UserAuthId == credential.Id)
            .Take(1000)
            .AsNoTracking()
            .ToListAsync(cancellationToken: cancellationToken);
        if (!result.Any())
        {
            return new ()
            {
                Message = $"No wallets exists",
                HttpStatusCode = HttpStatusCode.NoContent
            };
        }

        var response = result.Adapt<List<WalletResponse>>();
        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = response
        };
    }
}