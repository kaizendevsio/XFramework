using Wallets.Core.DataAccess.Commands.Entity.Wallets.Identity;

namespace Wallets.Core.DataAccess.Commands.Handlers.Wallets.Identity;

public class TransferWalletHandler : CommandBaseHandler, IRequestHandler<TransferWalletCmd, CmdResponseBO<TransferWalletCmd>>
{
    public TransferWalletHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponseBO<TransferWalletCmd>> Handle(TransferWalletCmd request, CancellationToken cancellationToken)
    {
        var initiatorCredentialEntity = await _dataLayer.TblIdentityCredentials.FirstOrDefaultAsync(i => i.Guid == $"{request.CredentialGuid}", cancellationToken);
        if (initiatorCredentialEntity == null)
        {
            return new ()
            {
                Message = $"Credential with Guid {request.FromCredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var fromCredentialEntity = await _dataLayer.TblIdentityCredentials.FirstOrDefaultAsync(i => i.Guid == $"{request.FromCredentialGuid}", cancellationToken);
        if (fromCredentialEntity == null)
        {
            return new ()
            {
                Message = $"Credential with Guid {request.FromCredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var toCredentialEntity = await _dataLayer.TblIdentityCredentials.FirstOrDefaultAsync(i => i.Guid == $"{request.ToCredentialGuid}", cancellationToken);
        if (toCredentialEntity == null)
        {
            return new ()
            {
                Message = $"Credential with Guid {request.ToCredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
            
        var fromWalletEntity = await _dataLayer.TblWalletEntities.FirstOrDefaultAsync(i => i.Guid == $"{request.FromWalletEntityGuid}", cancellationToken);
        if (fromWalletEntity == null)
        {
            return new ()
            {
                Message = $"Wallet entity with Guid {request.FromWalletEntityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var toWalletEntity = await _dataLayer.TblWalletEntities.FirstOrDefaultAsync(i => i.Guid == $"{request.ToWalletEntityGuid}", cancellationToken);
        if (toWalletEntity == null)
        {
            return new ()
            {
                Message = $"Wallet entity with Guid {request.ToWalletEntityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        if (request.Amount == 0)
        {
            return new ()
            {
                Message = $"Amount is required",
                HttpStatusCode = HttpStatusCode.BadRequest
            };
        }
        
        if (request.Amount > 99999999)
        {
            return new ()
            {
                Message = $"Amount exceeds maximum allowed",
                HttpStatusCode = HttpStatusCode.BadRequest
            };
        }
        
        var fromUserWallet = await _dataLayer.TblUserWallets
            .Where(i => i.UserAuthId == fromCredentialEntity.Id)
            .Where(i => i.WalletTypeId == fromWalletEntity.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (fromUserWallet == null)
        {
            return new ()
            {
                Message = $"Wallet with entity Guid {request.FromWalletEntityGuid} and credential Guid {request.FromCredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
            
        var toUserWallet = await _dataLayer.TblUserWallets
            .Where(i => i.UserAuthId == toCredentialEntity.Id)
            .Where(i => i.WalletTypeId == toWalletEntity.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (toUserWallet == null)
        {
            return new ()
            {
                Message = $"Wallet with entity Guid {request.ToWalletEntityGuid} and credential Guid {request.ToCredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        if (request.Amount > fromUserWallet.Balance)
        {
            return new ()
            {
                Message = $"Amount exceeds available balance",
                HttpStatusCode = HttpStatusCode.BadRequest
            };
        }
        _dataLayer.TblUserWalletTransactions.Add(new ()
        {
            UserAuthId = initiatorCredentialEntity.Id,
            Amount = request.Amount,
            SourceUserWalletId = fromUserWallet.Id,
            TargetUserWalletId = toUserWallet.Id,
            RunningBalance = fromUserWallet.Balance + request.Amount,
            PreviousBalance = fromUserWallet.Balance,
            Remarks = request.Remarks
        });
            
        fromUserWallet.Balance -= request.Amount;
        toUserWallet.Balance += request.Amount;
            
        _dataLayer.Update(fromUserWallet);
        _dataLayer.Update(toUserWallet);
        await _dataLayer.SaveChangesAsync(cancellationToken);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = $"You transferred {request.Amount} to Wallet Guid:{toUserWallet.Guid}"
        };
        
    }
}