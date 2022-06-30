using Wallets.Core.DataAccess.Commands.Entity.Wallets.Identity;

namespace Wallets.Core.DataAccess.Commands.Handlers.Wallets.Identity;

public class DecrementWalletHandler : CommandBaseHandler, IRequestHandler<DecrementWalletCmd, CmdResponse<DecrementWalletCmd>>
{
    public DecrementWalletHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
        
    public async Task<CmdResponse<DecrementWalletCmd>> Handle(DecrementWalletCmd request, CancellationToken cancellationToken)
    {
        var credentialEntity = await _dataLayer.IdentityCredentials.FirstOrDefaultAsync(i => i.Guid == $"{request.CredentialGuid}", cancellationToken);
        if (credentialEntity == null)
        {
            return new ()
            {
                Message = $"Credential with Guid {request.CredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var walletEntity = await _dataLayer.WalletEntities.FirstOrDefaultAsync(i => i.Guid == $"{request.WalletEntityGuid}", cancellationToken);
        if (walletEntity == null)
        {
            return new ()
            {
                Message = $"Wallet entity with Guid {request.CredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var entity = await _dataLayer.Wallets
            .Where(i => i.IdentityCredentialId == credentialEntity.Id)
            .Where(i => i.WalletEntityId == walletEntity.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (entity == null)
        {
            return new ()
            {
                Message = $"Credential Guid: {request.CredentialGuid} with Wallet Guid: {request.WalletEntityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
            
        entity.Balance -= request.Amount;
        _dataLayer.Update(entity);
        await _dataLayer.SaveChangesAsync(cancellationToken);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = $"{request.Amount} has been deducted to Wallet ID:{request.WalletEntityGuid}"
        };
    }
}