using PaymentGateways.Domain.Generic.Contracts.Requests.Create;
using PaymentGateways.Domain.Generic.Contracts.Responses;
using PaymentGateways.Integration.Interfaces;
using Wallets.Core.DataAccess.Commands.Entity.Wallets.Identity;
using XFramework.Integration.Interfaces;

namespace Wallets.Core.DataAccess.Commands.Handlers.Wallets.Identity;

public class CreateWalletDepositHandler : CommandBaseHandler, IRequestHandler<CreateWalletDepositCmd, CmdResponse<DepositResponse>>
{
    private readonly IHelperService _helperService;
    private readonly IPaymentGatewayWrapper _paymentGatewayWrapper;

    public CreateWalletDepositHandler(IDataLayer dataLayer, IHelperService helperService, IPaymentGatewayWrapper paymentGatewayWrapper)
    {
        _helperService = helperService;
        _paymentGatewayWrapper = paymentGatewayWrapper;
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DepositResponse>> Handle(CreateWalletDepositCmd request, CancellationToken cancellationToken)
    {
        var wrapperRequest = request.Adapt<CreateDepositRequest>();
        wrapperRequest.ClientReference = _helperService.GenerateReferenceString();
              
        var credential = await _dataLayer.IdentityCredentials.FirstOrDefaultAsync(i => i.Guid == $"{request.CredentialGuid}", cancellationToken);
        if (credential == null)
        {
            return new()
            {
                Message = $"Credential with Guid '{request.CredentialGuid}' does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
            
        var walletEntity = await _dataLayer.WalletEntities.FirstOrDefaultAsync(i => i.Guid == $"{request.WalletEntityGuid}", cancellationToken: cancellationToken);
        if (walletEntity == null)
        {
            return new()
            {
                Message = $"Wallet entity with code '{request.WalletEntityGuid}' does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var identityWallet = await _dataLayer.Wallets
            .Where(i => i.IdentityCredentialId == credential.Id)
            .Where(i => i.WalletEntityId == walletEntity.Id)
            .FirstOrDefaultAsync(CancellationToken.None);
        if (identityWallet == null)
        {
            return new()
            {
                Message = $"Wallet with user credential '{credential.Id}' does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        if (request.Amount <= 0)
        {
            return new()
            {
                Message = $"Amount must be greater than 0",
                HttpStatusCode = HttpStatusCode.BadRequest
            };
        }

        wrapperRequest.WalletEntityGuid = Guid.Parse(walletEntity.Guid);
        var result = await _paymentGatewayWrapper.CreateDepositTransaction(wrapperRequest).ConfigureAwait(false);

        return result.Adapt<CmdResponse<DepositResponse>>();
    }
}