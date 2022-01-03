using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using LoadManna.Integration.Interfaces.Wrappers;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentGateways.Domain.Generic.Contracts.Requests;
using Wallets.Core.DataAccess.Query.Entity.Wallets.Identity;
using Wallets.Core.Interfaces;
using Wallets.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.BusinessObjects;

namespace Wallets.Core.DataAccess.Query.Handlers.Wallets.Identity
{
    public class CreateIdentityWalletDepositHandler : QueryBaseHandler, IRequestHandler<CreateIdentityWalletDepositQuery, QueryResponseBO<WalletDepositContract>>
    {
        private readonly IPaymentGatewayWrapper _paymentGatewayWrapper;

        public CreateIdentityWalletDepositHandler(IDataLayer dataLayer, IPaymentGatewayWrapper paymentGatewayWrapper)
        {
            _paymentGatewayWrapper = paymentGatewayWrapper;
            _dataLayer = dataLayer;
        }

        public async Task<QueryResponseBO<WalletDepositContract>> Handle(CreateIdentityWalletDepositQuery request, CancellationToken cancellationToken)
        {
            var wrapperRequest = request.Adapt<CreatePaymentRequest>();
            wrapperRequest.TransactionGuid = Guid.NewGuid();
            
            var identityEntity = await _dataLayer.TblIdentityCredentials.FirstOrDefaultAsync(i => i.Cuid == request.Cuid, cancellationToken);
            if (identityEntity == null)
            {
                return new()
                {
                    Message = $"Identity with CUID {request.Cuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            
            var entity = await _dataLayer.TblWalletEntities.FirstOrDefaultAsync(i => i.Code == request.WalletTypeCode, cancellationToken: cancellationToken);
            if (entity == null)
            {
                return new()
                {
                    Message = $"Wallet Entity with code {request.WalletTypeCode} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            wrapperRequest.WalletTypeId = entity.Id;
            var result = await _paymentGatewayWrapper.CreateTransaction(wrapperRequest).ConfigureAwait(false);

            return result;
        }
    }
}