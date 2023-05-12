using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentGateways.Core.DataAccess.Commands.Entity.Transactions.Providers;
using PaymentGateways.Core.Interfaces;
using PaymentGateways.Domain.BusinessObjects.EcLink;
using PaymentGateways.Domain.Contracts.Requests.EcLink;

namespace PaymentGateways.Core.DataAccess.Commands.Handlers.Transactions.Providers;

public class ComposeEcLinkTransactionHandler : CommandBaseHandler, IRequestHandler<ComposeEcLinkTransactionCmd, QueryResponse<string>>
{
    private readonly ICachingService _cachingService;

    public ComposeEcLinkTransactionHandler(IDataLayer dataLayer,ICachingService cachingService)
    {
        _cachingService = cachingService;
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<string>> Handle(ComposeEcLinkTransactionCmd request, CancellationToken cancellationToken)
    {
        var configuration = await GetServiceConfiguration(cancellationToken);
        var transactionRequest1 = new EcLinkTransactionRequest()
        {
            EcLinkTransactionRequestHeader = new()
            {
                EcLinkTransactionRequestAuthHeader = new()
                {
                    MerchantCode = configuration.MerchantCode,
                    MerchantKey = configuration.MerchantKey
                }
            },
            EcLinkTransactionRequestBody = new()
            {
                EcLinkTransactionRequestCommitPayment = new()
                {
                    Amount = request.Request.Amount.ToString(),
                    Referenceno = request.ClientReference,
                    Expirydate = DateTime.Now.AddHours(2).AddHours(8).ToString("MM/dd/yyyy HH:mm:ss"),
                    Remarks = string.IsNullOrEmpty(request.Request.Description) ? string.Empty : request.Request.Description.Trim() 
                }
            }
        };
       
        var requestContent = transactionRequest1.ToXML();
        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = requestContent
        };
    }
    
    private async Task<EcPayConfiguration> GetServiceConfiguration(CancellationToken cancellationToken)
    {
        if (_cachingService.EcPayConfiguration != null) return _cachingService.EcPayConfiguration;
        var ecPayConfig = await _dataLayer.RegistryConfigurationGroups
            .AsNoTracking()
            .Include(i => i.RegistryConfigurations)
            .FirstOrDefaultAsync(i => i.Name == "ECPayApi", cancellationToken);

        _cachingService.EcPayConfiguration = new EcPayConfiguration()
        {
            MerchantCode = ecPayConfig.RegistryConfigurations.FirstOrDefault(i => i.Key == "MerchantCode").Value,
            MerchantKey = ecPayConfig.RegistryConfigurations.FirstOrDefault(i => i.Key == "MerchantKey").Value
        };

        return _cachingService.EcPayConfiguration;
    }
}