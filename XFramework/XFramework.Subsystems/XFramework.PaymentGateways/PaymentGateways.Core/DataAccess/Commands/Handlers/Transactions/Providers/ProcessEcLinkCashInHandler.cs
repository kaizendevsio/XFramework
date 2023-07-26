using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using MediatR;
using Messaging.Domain.Generic.Contracts.Requests.Create;
using Messaging.Integration.Interfaces;
using Microsoft.EntityFrameworkCore;
using PaymentGateways.Core.DataAccess.Commands.Entity.Transactions.Providers;
using PaymentGateways.Core.DataAccess.Query.Entity.Transactions;
using PaymentGateways.Core.Interfaces;
using PaymentGateways.Domain.Contracts.Responses;
using PaymentGateways.Domain.DataTransferObjects;
using XFramework.Domain.Generic.Enums;

namespace PaymentGateways.Core.DataAccess.Commands.Handlers.Transactions.Providers;

public class ProcessEcLinkCashInHandler : CommandBaseHandler,
    IRequestHandler<ProcessEcLinkCashInCmd, CmdResponse<ProcessTransactionResponse>>
{
    private readonly IMediator _mediator;
    private readonly IMessagingServiceWrapper _messagingServiceWrapper;

    public ProcessEcLinkCashInHandler(IDataLayer dataLayer, IMediator mediator,  IMessagingServiceWrapper messagingServiceWrapper)
    {
        _mediator = mediator;
        _messagingServiceWrapper = messagingServiceWrapper;
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<ProcessTransactionResponse>> Handle(ProcessEcLinkCashInCmd request, CancellationToken cancellationToken)
    {
        var transactionRequest = await _mediator.Send(new ComposeEcLinkTransactionCmd() {Request = request, ClientReference = request.ReferenceNumber});
        var result = await SendRequest(request.Gateway, transactionRequest.Response);
        
        var responseString = XmlToJsonResponseString(result, out var rawResponse, cancellationToken);
        var response = await HandleResponse(responseString);

        if (response.HttpStatusCode is HttpStatusCode.Accepted)
        {
            await SendNotification(new()
            {
                Gateway = request.Gateway,
                Discount = request.Gateway.Discount,
                Amount = request.Amount,
                SystemFee = request.Gateway.ServiceCharge,
                ConvenienceFee = request.Gateway.ConvenienceFee,
                ReferenceNumber = request.ReferenceNumber,
                ExpiryDate = DateTime.UtcNow.AddHours(24),
                CreatedAt = DateTime.UtcNow
            }, request.Credential);
        }
        
        return new()
        {
            HttpStatusCode = response.HttpStatusCode,
            Message = null,
            Response = new()
            {
                RawRequestData = transactionRequest.Response,
                RawResponseData = rawResponse,
                ReferenceNumber = request.ReferenceNumber,
            },
            
        };
    }

    private async Task<HttpResponseMessage> SendRequest(Gateway gateway, string requestContent)
    {
        using var client = new HttpClient()
        {
            BaseAddress = new Uri(gateway.ProviderEndpoint.BaseUrlEndpoint),
            Timeout = TimeSpan.FromHours(2)
        };
        //client.DefaultRequestHeaders.Add("SOAPAction", gateway.ProviderEndpoint.UrlEndpoint);

        var result = await client.PostAsync(new Uri(gateway.ProviderEndpoint.BaseUrlEndpoint), new StringContent(requestContent, Encoding.UTF8, "text/xml"), CancellationToken.None);
        return result;
    }
    private string XmlToJsonResponseString(HttpResponseMessage result, out string xmlResponseString, CancellationToken cancellationToken)
    {
        if (result.StatusCode is HttpStatusCode.Accepted)
        {
            xmlResponseString = "";
            return "";
        }
        
        var xmlNode = new XmlDocument();
        xmlResponseString = result.Content.ReadAsStringAsync(cancellationToken).Result;
        xmlNode.LoadXml(xmlResponseString);

        var responseString = Newtonsoft.Json.JsonConvert.SerializeXmlNode(xmlNode);
        return responseString;
    }
    private async Task<CmdResponse> HandleResponse(string responseString)
    {
        return await _mediator.Send(new HandleEcLinkResponseQuery(){ ResponseString = responseString});
    }
    
    private async Task SendNotification(ProcessTransactionResponse response, IdentityCredential credential)
    {
        var r = _dataLayer.RegistryConfigurationGroups
            .Include(i => i.RegistryConfigurations)
            .AsNoTracking()
            .FirstOrDefault(i => i.Name == "MessagingService_Confirmations");
        if (r == null) return;

        // Compose & Send Dealer Copy
        var dealerMessage = r.RegistryConfigurations
            .Where(i => i.Key == $"PaymentGateway_CashIn_Initiate")
            .FirstOrDefault()?.Value;

        dealerMessage = dealerMessage?.Replace("|GatewayName|", response.Gateway.Name);
        dealerMessage = dealerMessage?.Replace("|ReferenceNo|", $"{response.ReferenceNumber}");
        dealerMessage = dealerMessage?.Replace("|ExpiryDate|", $"{response.ExpiryDate?.AddHours(8).ToString()}");
        dealerMessage = dealerMessage?.Replace("|Amount|", $"PHP {response.Amount:n2}");
          
        var smsRequest = new CreateDirectMessageRequest()
        {
            Sender = credential.Guid,
            Recipient = credential.IdentityContacts
                .Where(i => i.EntityId == (int) GenericContactType.Phone)
                .FirstOrDefault()?.Value ?? "",
            Message = dealerMessage
        };
        await _messagingServiceWrapper.CreateDirectMessage(smsRequest);
    }
}