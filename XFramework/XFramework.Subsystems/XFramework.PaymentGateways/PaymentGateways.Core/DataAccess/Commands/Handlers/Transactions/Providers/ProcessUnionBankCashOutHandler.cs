using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PaymentGateways.Core.DataAccess.Commands.Entity.Transactions.Providers;
using PaymentGateways.Core.Interfaces;
using PaymentGateways.Domain.Contracts.Requests.UnionBank;
using PaymentGateways.Domain.Contracts.Responses;
using RestSharp;
using XFramework.Integration.Interfaces;

namespace PaymentGateways.Core.DataAccess.Commands.Handlers.Transactions.Providers;

public class ProcessUnionBankCashOutHandler : CommandBaseHandler, IRequestHandler<ProcessUnionBankCashOutCmd, CmdResponse<ProcessTransactionResponse>>
{
    private readonly IHelperService _helperService;

    public ProcessUnionBankCashOutHandler(IDataLayer dataLayer, IHelperService helperService)
    {
        _helperService = helperService;
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<ProcessTransactionResponse>> Handle(ProcessUnionBankCashOutCmd request, CancellationToken cancellationToken)
    {
        var client = new RestClient();
        var restRequest = new RestRequest("https://api-uat.unionbankph.com/partners/sb/partners/v1/oauth2/token", Method.Post);
        restRequest.AddHeader("Content-Type", "application/x-www-form-urlencoded");
        restRequest.AddHeader("Accept", "application/json");
        restRequest.AddParameter("grant_type", "password");
        restRequest.AddParameter("client_id", "3132ae12-caef-4ced-bc23-ed1fc7346d13");
        restRequest.AddParameter("username", "partner_sb");
        restRequest.AddParameter("password", "p@ssw0rd");
        restRequest.AddParameter("scope", "instapay");
        var response = await client.ExecuteAsync(restRequest, CancellationToken.None);

        Console.WriteLine(response.Content);

        var transferRequest = new RestRequest("https://api-uat.unionbankph.com/partners/sb/partners/v3/instapay/transfers/single");
        transferRequest.AddHeader("x-ibm-client-id", "quis consectetur");
        transferRequest.AddHeader("x-ibm-client-secret", "quis consectetur");
        transferRequest.AddHeader("x-partner-id", "quis consectetur");
        transferRequest.AddHeader("Content-Type", "application/json");
        transferRequest.AddHeader("Accept", "application/json");

        var transferRequestData = new UnionBankInstaPayTransferRequest
        {
            SenderRefId = _helperService.GenerateReferenceString(),
            TranRequestDate = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
            Sender = new()
            {
                Name = request.Credential.IdentityInfo.FirstName + " " + request.Credential.IdentityInfo.LastName,
                Address = null
            },
            Beneficiary = new()
            {
                AccountNumber = null,
                Name = null,
                Address = null
            },
            Remittance = new()
            {
                Amount = null,
                Currency = null,
                ReceivingBank = null,
                Purpose = null,
                Instructions = null
            }
        };
        
        var body = JsonSerializer.Serialize(transferRequestData);
        transferRequest.AddParameter("application/json", body, ParameterType.RequestBody);
        
        var transferResponse = await client.ExecuteAsync(transferRequest, CancellationToken.None);
        Console.WriteLine(response.Content);

        return new();
    }
}