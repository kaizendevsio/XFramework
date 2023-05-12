using PaymentGateways.Domain.Generic.Contracts.Requests.Create;
using PaymentGateways.Integration.Interfaces;
using Wallets.Core.DataAccess.Commands.Entity.Wallets.Identity;
using XFramework.Integration.Interfaces;

namespace Wallets.Core.DataAccess.Commands.Handlers.Wallets.Identity;

public class CreateWithdrawalHandler : CommandBaseHandler, IRequestHandler<CreateWalletWithdrawalCmd, CmdResponse>
{
    private readonly IHelperService _helperService;
    private readonly IPaymentGatewayWrapper _paymentGatewayWrapper;

    public CreateWithdrawalHandler(IDataLayer dataLayer, IHelperService helperService, IPaymentGatewayWrapper paymentGatewayWrapper)
    {
        _helperService = helperService;
        _paymentGatewayWrapper = paymentGatewayWrapper;
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse> Handle(CreateWalletWithdrawalCmd request, CancellationToken cancellationToken)
    {
        var wrapperRequest = request.Adapt<CreateWithdrawalRequest>();
        wrapperRequest.ClientReference = _helperService.GenerateReferenceString();
            
        var identityCredential = await _dataLayer.IdentityCredentials.FirstOrDefaultAsync(i => i.Guid == $"{request.CredentialGuid}", cancellationToken);
        if (identityCredential == null)
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
        
        var currencyEntity = await _dataLayer.CurrencyEntities.FirstOrDefaultAsync(i => i.CurrencyIsoCode3 == $"{request.Currency}", cancellationToken: cancellationToken);
        if (currencyEntity == null)
        {
            return new()
            {
                Message = $"Currency with code '{request.Currency}' does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        wrapperRequest.WalletEntityGuid = Guid.Parse(walletEntity.Guid);
        wrapperRequest.Currency = currencyEntity.CurrencyIsoCode3;
        var result = await _paymentGatewayWrapper.CreateWithdrawalTransaction(wrapperRequest).ConfigureAwait(false);

        return result;
    }
}