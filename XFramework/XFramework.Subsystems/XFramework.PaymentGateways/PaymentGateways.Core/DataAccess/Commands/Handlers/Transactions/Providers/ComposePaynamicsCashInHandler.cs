using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentGateways.Core.DataAccess.Commands.Entity.Transactions.Providers;
using PaymentGateways.Core.Interfaces;
using PaymentGateways.Domain.BusinessObjects.Paynamics;
using PaymentGateways.Domain.Contracts.Requests.Paynamics;
using XFramework.Integration.Security;

namespace PaymentGateways.Core.DataAccess.Commands.Handlers.Transactions.Providers;

public class ComposePaynamicsCashInHandler : CommandBaseHandler, IRequestHandler<ComposePaynamicsCashInCmd, QueryResponse<string>>
{
    private readonly ICachingService _cachingService;

    public ComposePaynamicsCashInHandler(IDataLayer dataLayer,ICachingService cachingService)
    {
        _cachingService = cachingService;
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<string>> Handle(ComposePaynamicsCashInCmd request, CancellationToken cancellationToken)
    {
        var configuration = await GetServiceConfiguration(cancellationToken);
        var transactionRequest = new PaynamicsTransactionRequest
        {
            Orders = new()
            {
                SubItems = new()
                {
                    Items = new()
                    {
                        new()
                        {
                            Itemname = $"Deposit Request: {request.Request.Description}",
                            Quantity = "1",
                            Amount = $"{request.Request.Amount}"
                        }
                    }
                }
            },
            Mid = configuration.MerchantId,
            Request_id = request.ClientReference,
            Notification_url = configuration.NotificationUrl,
            Response_url = configuration.ResponseUrl,
            Cancel_url = configuration.CancelUrl,
            Mtac_url = "",
            Descriptor_note = "",
            Amount = $"{request.Request.Amount:n2}",
            Secure3d = "try3d",
            Trxtype = "sale",
            Currency = "PHP",
            Mlogo_url = "https://retailer.loadmanna.ph/assets/common/img/app_logo.png",
            Pmethod = "",
            Signature = "",
            Ip_address = request.Request.RequestServer.IpAddress,
            Fname = request.IdentityInformation.FirstName,
            Mname = "",
            Lname = request.IdentityInformation.LastName,
            Address1 = "",
            Address2 = "",
            City = "",
            State = "",
            Country = "",
            Zip = "",
            Email = "",
            Phone = "",
            Mobile = "",
            Client_ip = request.Request.RequestServer.IpAddress
        };

        var signature = transactionRequest.Adapt<PaynamicsSignature>();
        signature.Merchant_Key = configuration.MerchantKey;
        var hashString = signature.ToString().ToSha512();
        
        transactionRequest.Signature = hashString;
        var requestContent = transactionRequest.ToXml();
        
        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = requestContent.ToBase64()
        };
    }
    
    private async Task<PaynamicsConfiguration> GetServiceConfiguration(CancellationToken cancellationToken)
    {
        if (_cachingService.PaynamicsConfiguration != null) return _cachingService.PaynamicsConfiguration;
        var config = await _dataLayer.RegistryConfigurationGroups
            .AsNoTracking()
            .Include(i => i.RegistryConfigurations)
            .FirstOrDefaultAsync(i => i.Name == "PaynamicsApi", cancellationToken);

        _cachingService.PaynamicsConfiguration = new PaynamicsConfiguration
        {
            MerchantId = config.RegistryConfigurations.FirstOrDefault(i => i.Key == "MerchantId").Value,
            MerchantKey = config.RegistryConfigurations.FirstOrDefault(i => i.Key == "MerchantKey").Value,
            CancelUrl = config.RegistryConfigurations.FirstOrDefault(i => i.Key == "CancelUrl").Value,
            NotificationUrl = config.RegistryConfigurations.FirstOrDefault(i => i.Key == "NotificationUrl").Value,
            ResponseUrl = config.RegistryConfigurations.FirstOrDefault(i => i.Key == "ResponseUrl").Value
        };

        return _cachingService.PaynamicsConfiguration;
    }
}