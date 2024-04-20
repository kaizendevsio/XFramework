using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PaymentGateways.Core.DataAccess.Commands.Entity.Transactions.Providers;
using PaymentGateways.Core.Interfaces;
using PaymentGateways.Domain.Contracts.Responses;
using PaymentGateways.Domain.Contracts.Responses.UnionBank;
using RestSharp;
using Wallets.Integration.Interfaces;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Interfaces.Wrappers;

namespace PaymentGateways.Core.DataAccess.Commands.Handlers.Transactions.Providers;

public class ProcessUnionBankCashInHandler : CommandBaseHandler, IRequestHandler<ProcessUnionBankCashInCmd, CmdResponse<ProcessTransactionResponse>>
{
    private readonly IWalletServiceWrapper _walletServiceWrapper;
    private readonly IHelperService _helperService;

    public ProcessUnionBankCashInHandler(IDataLayer dataLayer, IWalletServiceWrapper walletServiceWrapper, IHelperService helperService)
    {
        _walletServiceWrapper = walletServiceWrapper;
        _helperService = helperService;
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<ProcessTransactionResponse>> Handle(ProcessUnionBankCashInCmd request, CancellationToken cancellationToken)
    {
        
        var client = new RestClient();
        var restRequest = new RestRequest("https://api-uat.unionbankph.com/partners/sb/partners/v1/oauth2/token", Method.Post);
        restRequest.AddHeader("Content-Type", "application/x-www-form-urlencoded");
        restRequest.AddHeader("Accept", "application/json");
        restRequest.AddParameter("grant_type", "password");
        restRequest.AddParameter("client_id", "2dd2bfce-5c49-4420-96e8-371dded12453");
        restRequest.AddParameter("username", "partner_sb");
        restRequest.AddParameter("password", "p@ssw0rd");
        restRequest.AddParameter("scope", "account_inquiry");
        var response = await client.ExecuteAsync<UnionBankTokenResponse>(restRequest, CancellationToken.None);

        Console.WriteLine(response.Content);

        var txHistory = new RestRequest("https://api-uat.unionbankph.com/partners/sb/portal/accounts/v1/transactions");
        txHistory.AddHeader("x-ibm-client-id", "2dd2bfce-5c49-4420-96e8-371dded12453");
        txHistory.AddHeader("x-ibm-client-secret", "eL4fD1uY8gR3mB5rB6eQ8lK1lN2lC5xF0qP6xL1nG6hV1cF0vT");
        txHistory.AddHeader("x-partner-id", "5dff2cdf-ef15-48fb-a87b-375ebff415bb");
        txHistory.AddHeader("Content-Type", "application/json");
        txHistory.AddHeader("Accept", "application/json");
        txHistory.AddHeader("Authorization", $"Bearer {response.Data.AccessToken}");
        
        txHistory.AddParameter("fromDate", DateTime.Parse($"{request.DepositDateTime}").ToString("yyyy-MM-dd"));
        txHistory.AddParameter("toDate", DateTime.Parse($"{request.DepositDateTime}").AddDays(1).ToString("yyyy-MM-dd"));
        txHistory.AddParameter("tranType", "C");
        txHistory.AddParameter("limit", "100");

        var transferResponse = await client.ExecuteAsync<UnionBankAccountTransactionResponse>(txHistory, CancellationToken.None);
        Console.WriteLine(transferResponse.Content);

        if (!transferResponse.IsSuccessful)
        {
            return new()
            {
                HttpStatusCode = transferResponse.StatusCode,
                Message = $"{transferResponse.StatusCode}",
                Response = new()
                {
                    RawRequestData = transferResponse.Request?.ToString(),
                    RawResponseData = transferResponse.Content
                }
            };
        }
        
        
        if (!transferResponse.Data.Records.Any(i => i.TranId == request.ReferenceNumber 
                                                    && decimal.Parse(i.Amount) == request.Amount
                                                    && i.PostedDate >= request.DepositDateTime.Subtract(TimeSpan.FromDays(1)) 
                                                    && i.PostedDate <= request.DepositDateTime.Add(TimeSpan.FromDays(1))
                                                    && i.TranType == "C"
                                                    ))
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = "Transaction not found",
                Response = new()
                {
                    RawRequestData = transferResponse.Request?.ToString(),
                    RawResponseData = transferResponse.Content
                }
            };
        }

        var incrementResponse = await _walletServiceWrapper.IncrementWallet(new()
        {
            ClientReference = request.ReferenceNumber,
            Description = request.Description,
            Currency = "PHP",
            CredentialGuid = Guid.Parse(request.Credential.Guid),
            WalletEntityGuid = request.Wallet.WalletEntity.Guid,
            Amount = request.Amount
        });

        if (incrementResponse.HttpStatusCode is not HttpStatusCode.Accepted)
        {
            return new()
            {
                HttpStatusCode = incrementResponse.HttpStatusCode,
                Message = incrementResponse.Message,
                Response = new()
                {
                    RawRequestData = transferResponse.Request?.ToString(),
                    RawResponseData = transferResponse.Content
                    // send sentry exception
                }
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Transaction found",
            Response = new()
            {
                RawRequestData = transferResponse.Request?.ToString(),
                RawResponseData = transferResponse.Content,
                ReferenceNumber = request.ReferenceNumber,
            }
        };
    }
    

}