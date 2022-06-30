using Wallets.Core.DataAccess.Commands.Entity.Wallets.Identity;
using XFramework.Integration.Interfaces.Wrappers;

namespace Wallets.Core.DataAccess.Commands.Handlers.Wallets.Identity;

public class TransferWalletHandler : CommandBaseHandler, IRequestHandler<TransferWalletCmd, CmdResponse<TransferWalletCmd>>
{
    private readonly IIdentityServiceWrapper _identityServiceWrapper;

    public TransferWalletHandler(IIdentityServiceWrapper identityServiceWrapper ,IDataLayer dataLayer, IMediator mediator)
    {
        _identityServiceWrapper = identityServiceWrapper;
        _mediator = mediator;
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<TransferWalletCmd>> Handle(TransferWalletCmd request, CancellationToken cancellationToken)
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
        
        var fromCredential = await _dataLayer.IdentityCredentials
            .Include(i => i.Wallets)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.CredentialGuid}", cancellationToken);
        if (fromCredential == null)
        {
            return new ()
            {
                Message = $"Credential with Guid {request.CredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        // Check Recipient Type
        var toCredential = new IdentityCredential();
        toCredential = Guid.TryParse(request.Recipient, out var credentialGuid)
            ? await _dataLayer.IdentityCredentials
                .Include(i => i.Wallets)
                .AsSplitQuery()
                .FirstOrDefaultAsync(i => i.Guid == $"{request.Recipient}", cancellationToken)
            : await _dataLayer.IdentityCredentials
                .Include(i => i.Wallets)
                .AsSplitQuery()
                .FirstOrDefaultAsync(i => i.UserName == $"{request.Recipient}", cancellationToken);
        
        if (toCredential == null)
        {
            // Try To Get Credential From Contact
            var credentialByContact = await _identityServiceWrapper.GetCredentialByContact(new() {ContactValue = request.Recipient});
            if (credentialByContact.HttpStatusCode is not HttpStatusCode.Accepted)
            {
                return new ()
                {
                    Message = $"Credential {request.Recipient} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                }; 
            }
        }
        
        var walletEntity = await _dataLayer.WalletEntities.FirstOrDefaultAsync(i => i.Guid == $"{request.WalletEntityGuid}", cancellationToken);
        if (walletEntity == null)
        {
            return new ()
            {
                Message = $"Wallet entity with Guid {request.WalletEntityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var fromUserWallet = fromCredential.Wallets
            .Where(i => i.WalletEntityId == walletEntity.Id)
            .FirstOrDefault();
        if (fromUserWallet == null)
        {
            return new ()
            {
                Message = $"Credential with guid '{fromCredential.Guid}' does not have wallet with wallet entity guid '{request.WalletEntityGuid}'",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
            
        var toUserWallet = toCredential.Wallets
            .Where(i => i.WalletEntityId == walletEntity.Id)
            .FirstOrDefault();
        if (toUserWallet == null)
        {
            return new ()
            {
                Message = $"Credential with guid '{toCredential.Guid}' does not have wallet with wallet entity guid '{request.WalletEntityGuid}'",
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
            IdentityCredential = fromCredential,
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