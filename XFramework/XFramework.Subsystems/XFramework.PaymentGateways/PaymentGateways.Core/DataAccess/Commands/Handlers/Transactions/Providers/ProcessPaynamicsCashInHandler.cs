using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PaymentGateways.Core.DataAccess.Commands.Entity.Transactions.Providers;
using PaymentGateways.Core.Interfaces;
using PaymentGateways.Domain.Contracts.Responses;

namespace PaymentGateways.Core.DataAccess.Commands.Handlers.Transactions.Providers;

public class ProcessPaynamicsCashInHandler : CommandBaseHandler, IRequestHandler<ProcessPaynamicsCashInCmd, CmdResponse<ProcessTransactionResponse>>
{
    private readonly IMediator _mediator;

    public ProcessPaynamicsCashInHandler(IDataLayer dataLayer, IMediator mediator)
    {
        _mediator = mediator;
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<ProcessTransactionResponse>> Handle(ProcessPaynamicsCashInCmd request, CancellationToken cancellationToken)
    {
        var transactionRequest = await _mediator.Send(new ComposePaynamicsCashInCmd() {Request = request, ClientReference = request.ReferenceNumber});
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Transaction accepted",
            Response = new()
            {
                RawRequestData = transactionRequest.Response,
                RawResponseData = ""
            },
            IsSuccess = false
        };
    }
}