using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentGateways.Core.DataAccess.Commands.Entity.Transactions;
using PaymentGateways.Core.DataAccess.Commands.Entity.Transactions.Providers;
using PaymentGateways.Core.Interfaces;
using PaymentGateways.Domain.Contracts.Responses;
using PaymentGateways.Domain.DataTransferObjects;
using PaymentGateways.Domain.Generic.Contracts.Responses;
using Wallets.Domain.Generic.Contracts.Responses;
using Wallets.Integration.Interfaces;
using XFramework.Domain.Generic.Enums;
using XFramework.Integration.Interfaces;

namespace PaymentGateways.Core.DataAccess.Commands.Handlers.Transactions
{
    public class CreateDepositHandler : CommandBaseHandler, IRequestHandler<CreateDepositCmd, CmdResponse<DepositResponse>>
    {
        private readonly IHelperService _helperService;
        private readonly IMediator _mediator;
        private readonly IWalletServiceWrapper _walletServiceWrapper;

        public CreateDepositHandler(IDataLayer dataLayer, IHelperService helperService, IMediator mediator, IWalletServiceWrapper walletServiceWrapper)
        {
            _helperService = helperService;
            _mediator = mediator;
            _walletServiceWrapper = walletServiceWrapper;
            _dataLayer = dataLayer;
        }
        
        public virtual async Task<CmdResponse<DepositResponse>> Handle(CreateDepositCmd request, CancellationToken cancellationToken)
        {
            ValidateRequest(request);
        
            var credential = await GetIdentityCredential(request);
            var currency = await GetCurrency(request);
            var wallet = await GetIdentityWallet(credential, request);
            var gateway = await GetGateway(request);
            var response = await ProcessTransaction(request, gateway, credential, wallet);

            await RecordTransactionToDatabase(credential, wallet, currency, request, response);

            if (response.HttpStatusCode is not HttpStatusCode.Accepted) return response.Adapt<CmdResponse<DepositResponse>>();
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Message = "Transaction processed successfully",
                Response = ConvertToResponse(response.Response)
            };
        }

        private void ValidateRequest(CreateDepositCmd request)
        {
            if (request.Amount <= 0) throw new ArgumentException("Amount must be greater than zero");
            if (string.IsNullOrEmpty(request.Description)) throw new ArgumentException("Description is required");
        }
    
        private async Task<CurrencyEntity> GetCurrency(CreateDepositCmd request)
        {
            var currencyEntity = await _dataLayer.CurrencyEntities.FirstOrDefaultAsync(i => i.CurrencyIsoCode3 == $"{request.Currency}");
            if (currencyEntity == null) throw new Exception("Currency not found");
            return currencyEntity;
        }

        private async Task RecordTransactionToDatabase(IdentityCredential credential, WalletResponse wallet, CurrencyEntity currency, CreateDepositCmd request, QueryResponse<ProcessTransactionResponse> response)
        {
            var txRecord = new DepositRequest()
            {
                GatewayId = response.Response.Gateway.Id,
                Amount = request.Amount,
                DepositStatus = response.HttpStatusCode == HttpStatusCode.BadRequest
                    ? (short?) TransactionStatus.Failed
                    : (short?) TransactionStatus.Pending,
                RawRequestData = response.Response.RawRequestData,
                RawResponseData = response.Response.RawResponseData,
                ReferenceNo = response.Response.ReferenceNumber,
                Remarks = request.Description,
                SourceCurrencyId = currency.Id,
                IdentityCredentialId = credential.Id,
                TargetWalletTypeId = wallet.WalletTypeId,
                ExpiryDate = response.Response.ExpiryDate,
                Discount = response.Response.Discount,
                ConvenienceFee = response.Response.ConvenienceFee,
                SystemFee = response.Response.ConvenienceFee,
                DiscountType = (int?) DiscountType.Value,
            };
            await _dataLayer.DepositRequests.AddAsync(txRecord);
            await _dataLayer.SaveChangesAsync();
        }

        private async Task<QueryResponse<ProcessTransactionResponse>> ProcessTransaction(CreateDepositCmd request, Gateway gateway, IdentityCredential credential, WalletResponse wallet)
        {
            var response = new CmdResponse<ProcessTransactionResponse>();
            var clientReferenceNo = $"{_helperService.GenerateRandomNumber(111111,999999)}{credential.IdentityContacts.Where(i => i.Entity.Name.Equals("Phone")).FirstOrDefault()?.Value.Substring(4)}";

            response = gateway.ProviderEndpoint.Gateway.Name switch
            {
                "UnionBank" => await _mediator.Send(new ProcessUnionBankCashInCmd
                {
                    ReferenceNumber = request.ClientReference,
                    Credential = credential,
                    Gateway = gateway,
                    Amount = request.Amount,
                    Description = request.Description,
                    DepositDateTime = request.DepositDate,
                    Wallet = wallet
                }),
                "Paynamics" => await _mediator.Send(new ProcessPaynamicsCashInCmd
                {
                    ReferenceNumber = clientReferenceNo,
                    Credential = credential,
                    Gateway = gateway,
                    Amount = request.Amount,
                    Description = request.Description
                }),
                "ECLink" => await _mediator.Send(new ProcessEcLinkCashInCmd
                {
                    ReferenceNumber = clientReferenceNo,
                    Credential = credential, 
                    Gateway = gateway,
                    Amount = request.Amount,
                    Description = request.Description
                })
            };

            if (response.HttpStatusCode is not HttpStatusCode.Accepted) throw new ArgumentException($"Error processing transaction: {response.Message}");

            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Message = "Transaction processed successfully",
                IsSuccess = false,
                Response = new()
                {
                    Gateway = gateway,
                    Discount = gateway.Discount,
                    Amount = request.Amount,
                    SystemFee = gateway.ServiceCharge,
                    ConvenienceFee = gateway.ConvenienceFee,
                    RawRequestData = response.Response.RawRequestData,
                    RawResponseData = response.Response.RawResponseData,
                    ReferenceNumber = clientReferenceNo,
                    ExpiryDate = DateTime.UtcNow.AddHours(24),
                    CreatedAt = DateTime.UtcNow,
                }
            };

        }

        private async Task<IdentityCredential> GetIdentityCredential(CreateDepositCmd request)
        {
            var credential = await _dataLayer.IdentityCredentials
                .AsNoTracking()
                .Include(i => i.IdentityInfo)
                .Include(i => i.IdentityContacts)
                .ThenInclude(i => i.Entity)
                .FirstOrDefaultAsync(i => i.Guid == request.CredentialGuid.ToString(), CancellationToken.None);

            if (credential == null) throw new ArgumentException($"Credential with guid {request.CredentialGuid} does not exist");
            return credential;
        }

        private async Task<WalletResponse> GetIdentityWallet(IdentityCredential credential, CreateDepositCmd request)
        {
            var walletEntity = await _dataLayer.WalletEntities
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Guid == $"{request.WalletEntityGuid}", CancellationToken.None);
            
            if (walletEntity == null) throw new ArgumentException($"Wallet type {request.WalletEntityGuid} does not exist");
            var identityWallet = await _walletServiceWrapper.GetWalletList(new()
            {
                CredentialGuid = Guid.Parse(credential.Guid),
            });

            if (identityWallet.HttpStatusCode is not HttpStatusCode.Accepted) throw new ArgumentException($"Failed to get wallet list for identity {credential.Guid}: {identityWallet.Message}");
            
            var wallet = identityWallet.Response.FirstOrDefault(i => i.WalletEntity.Guid == request.WalletEntityGuid);
            if (wallet == null) throw new ArgumentException($"Wallet with type {request.WalletEntityGuid} does not exist");
            
            return wallet;
        }

        private async Task<Gateway> GetGateway(CreateDepositCmd request)
        {
            var gateway = await _dataLayer.Gateways
                .AsNoTracking()
                .Include(i => i.ProviderEndpoint)
                .ThenInclude(i => i.Gateway)
                .FirstOrDefaultAsync(i => i.Guid == $"{request.GatewayGuid}", CancellationToken.None);

            if (gateway == null) throw new ArgumentException($"Gateway does not exist");
            return gateway;
        }
        
        private DepositResponse ConvertToResponse(ProcessTransactionResponse response)
        {
            return new()
            {
                GatewayName = response.Gateway.Name,
                Transaction = null,
                Amount = response.Amount,
                TotalAmount = response.Amount + response.ConvenienceFee + response.SystemFee + (response.Discount ?? 0),
                ServiceFee = response.SystemFee,
                ConvenienceFee = response.ConvenienceFee,
                Discount = response.Discount ?? 0,
                ExpiryDate = response.ExpiryDate ?? DateTime.MaxValue,
                CreatedAt = response.CreatedAt,
                Result = response.ReferenceNumber
            };
        }
    }
}