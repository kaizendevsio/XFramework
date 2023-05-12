using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PaymentGateways.Core.DataAccess.Commands.Handlers;
using PaymentGateways.Core.DataAccess.Query.Entity.Transactions;
using PaymentGateways.Domain.Contracts.Responses.EcLink;
using XFramework.Integration.Interfaces.Wrappers;

namespace PaymentGateways.Core.DataAccess.Query.Handlers.Transactions;

public class HandleEcLinkResponseHandler : CommandBaseHandler, IRequestHandler<HandleEcLinkResponseQuery, CmdResponse>
{
    private readonly ILoggerWrapper _logger;

    public HandleEcLinkResponseHandler(ILoggerWrapper logger)
    {
        _logger = logger;
    }
    
    public async Task<CmdResponse> Handle(HandleEcLinkResponseQuery request, CancellationToken cancellationToken)
    {
        var responseObject = JsonSerializer.Deserialize<EcLinkTransactionContract>(request.ResponseString);
        if (responseObject.SoapEnvelope.SoapBody.CommitPaymentResponse == null || string.IsNullOrEmpty(responseObject.TransactResult.First().ResultCode) || int.Parse(responseObject.TransactResult.First().ResultCode) != 0)
        {
            _logger.NewLog("EcLink API Exception", request.ResponseString, "PaymentGateway Service : CreateCashInTransactionHandler", request.RequestServer);
            return new ()
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = responseObject.SoapEnvelope.SoapBody.CommitPaymentResponse == null ? "Internal Server Error" : responseObject.TransactResult.First().Result
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = responseObject.TransactResult.First().Result
        };
    }
}