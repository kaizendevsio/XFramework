﻿using Wallets.Core.DataAccess.Commands.Entity.Wallets.Identity;

namespace Wallets.Core.DataAccess.Commands.Handlers.Wallets.Identity;

public class IncrementWalletHandler : CommandBaseHandler, IRequestHandler<IncrementWalletCmd, CmdResponseBO<IncrementWalletCmd>>
{
    public IncrementWalletHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
        
    public async Task<CmdResponseBO<IncrementWalletCmd>> Handle(IncrementWalletCmd request, CancellationToken cancellationToken)
    {
        var credentialEntity = await _dataLayer.TblIdentityCredentials.FirstOrDefaultAsync(i => i.Guid == $"{request.CredentialGuid}", cancellationToken);
        if (credentialEntity == null)
        {
            return new ()
            {
                Message = $"Credential with Guid {request.CredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var walletEntity = await _dataLayer.TblWalletEntities.FirstOrDefaultAsync(i => i.Guid == $"{request.WalletEntityGuid}", cancellationToken);
        if (walletEntity == null)
        {
            return new ()
            {
                Message = $"Wallet entity with Guid {request.CredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var entity = await _dataLayer.TblUserWallets
            .Where(i => i.UserAuthId == credentialEntity.Id)
            .Where(i => i.WalletTypeId == walletEntity.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (entity == null)
        {
            return new ()
            {
                Message = $"Credential Guid: {request.CredentialGuid} with Wallet Guid: {request.WalletEntityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
            
        entity.Balance += request.Amount;
        _dataLayer.Update(entity);
        await _dataLayer.SaveChangesAsync(cancellationToken);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = $"{request.Amount} has been added to Wallet ID:{request.WalletEntityGuid}"
        };
    }
        
        
        
}