using LoadManna.Integration.Interfaces.Wrappers;
using PaymentGateways.Domain.Generic.Contracts.Requests;
using Wallets.Core.DataAccess.Query.Entity.Wallets.Identity;

namespace Wallets.Core.DataAccess.Query.Handlers.Wallets.Identity;

public class CreateWalletDepositHandler : QueryBaseHandler, IRequestHandler<CreateWalletDepositQuery, QueryResponse<WalletDepositResponse>>
{
    private readonly IPaymentGatewayWrapper _paymentGatewayWrapper;

    public CreateWalletDepositHandler(IDataLayer dataLayer, IPaymentGatewayWrapper paymentGatewayWrapper)
    {
        _paymentGatewayWrapper = paymentGatewayWrapper;
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<WalletDepositResponse>> Handle(CreateWalletDepositQuery request, CancellationToken cancellationToken)
    {
        var wrapperRequest = request.Adapt<CreatePaymentRequest>();
        wrapperRequest.TransactionGuid = Guid.NewGuid();
            
        var identityEntity = await _dataLayer.TblIdentityCredentials.FirstOrDefaultAsync(i => i.Guid == $"{request.CredentialGuid}", cancellationToken);
        if (identityEntity == null)
        {
            return new()
            {
                Message = $"Credential with Guid {request.CredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
            
        var entity = await _dataLayer.TblWalletEntities.FirstOrDefaultAsync(i => i.Guid == $"{request.WalletEntityGuid}", cancellationToken: cancellationToken);
        if (entity == null)
        {
            return new()
            {
                Message = $"Wallet entity with code {request.WalletEntityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        wrapperRequest.WalletTypeId = entity.Id;
        var result = await _paymentGatewayWrapper.CreateTransaction(wrapperRequest).ConfigureAwait(false);

        return new();
        //return result;
    }
}