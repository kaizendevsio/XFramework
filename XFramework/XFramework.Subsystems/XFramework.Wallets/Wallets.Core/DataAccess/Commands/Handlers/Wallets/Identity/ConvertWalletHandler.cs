using Wallets.Core.DataAccess.Commands.Entity.Wallets.Identity;

namespace Wallets.Core.DataAccess.Commands.Handlers.Wallets.Identity;

public class ConvertWalletHandler : CommandBaseHandler, IRequestHandler<ConvertWalletCmd, CmdResponse<ConvertWalletCmd>>
{
    public ConvertWalletHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<ConvertWalletCmd>> Handle(ConvertWalletCmd request, CancellationToken cancellationToken)
    {
        switch (request.Amount)
        {
            case <= 0:
                return new ()
                {
                    Message = $"Amount is required",
                    HttpStatusCode = HttpStatusCode.BadRequest
                };
            case > 9_999_999_999:
                return new ()
                {
                    Message = $"Amount exceeds maximum allowed",
                    HttpStatusCode = HttpStatusCode.BadRequest
                };
        }

        var initiatorCredential = await _dataLayer.IdentityCredentials.FirstOrDefaultAsync(i => i.Guid == $"{request.CredentialGuid}", cancellationToken);
        if (initiatorCredential == null)
        {
            return new ()
            {
                Message = $"Credential with Guid {request.CredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var fromCredential = await _dataLayer.IdentityCredentials
            .Include(i => i.Wallets)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.FromCredentialGuid}", cancellationToken);
        if (fromCredential == null)
        {
            return new ()
            {
                Message = $"Credential with Guid {request.FromCredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var toCredential = await _dataLayer.IdentityCredentials
            .Include(i => i.Wallets)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.ToCredentialGuid}", cancellationToken);
        if (toCredential == null)
        {
            return new ()
            {
                Message = $"Credential with Guid {request.ToCredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
            
        var fromWalletEntity = await _dataLayer.WalletEntities.FirstOrDefaultAsync(i => i.Guid == $"{request.FromWalletEntityGuid}", cancellationToken);
        if (fromWalletEntity == null)
        {
            return new ()
            {
                Message = $"Wallet entity with Guid {request.FromWalletEntityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var toWalletEntity = await _dataLayer.WalletEntities.FirstOrDefaultAsync(i => i.Guid == $"{request.ToWalletEntityGuid}", cancellationToken);
        if (toWalletEntity == null)
        {
            return new ()
            {
                Message = $"Wallet entity with Guid {request.ToWalletEntityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var fromUserWallet = fromCredential.Wallets
            .Where(i => i.WalletEntityId == fromWalletEntity.Id)
            .FirstOrDefault();
        if (fromUserWallet == null)
        {
            return new ()
            {
                Message = $"Wallet with entity Guid {request.FromWalletEntityGuid} and credential Guid {request.FromCredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
            
        var toUserWallet = toCredential.Wallets
            .Where(i => i.WalletEntityId == toWalletEntity.Id)
            .FirstOrDefault();
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
        _dataLayer.WalletTransactions.Add(new ()
        {
            IdentityCredential = initiatorCredential,
            Amount = request.Amount,
            SourceUserWallet = fromUserWallet,
            TargetUserWallet = toUserWallet,
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