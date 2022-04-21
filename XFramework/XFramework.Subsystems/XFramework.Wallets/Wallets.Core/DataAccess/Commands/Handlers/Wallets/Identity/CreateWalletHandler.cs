using Wallets.Core.DataAccess.Commands.Entity.Wallets.Identity;

namespace Wallets.Core.DataAccess.Commands.Handlers.Wallets.Identity;

public class CreateWalletHandler  : CommandBaseHandler, IRequestHandler<CreateWalletCmd, CmdResponse<CreateWalletCmd>>
{
    public CreateWalletHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
        
    public async Task<CmdResponse<CreateWalletCmd>> Handle(CreateWalletCmd request, CancellationToken cancellationToken)
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
                Message = $"Wallet with Guid {request.CredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var entity = request.Adapt<Wallet>();
        entity.IdentityCredential = credentialEntity;
        entity.WalletEntity = walletEntity;
        
        _dataLayer.Wallets.Add(entity);
        await _dataLayer.SaveChangesAsync(cancellationToken);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Wallet identity created successfully"
        };
    }
}