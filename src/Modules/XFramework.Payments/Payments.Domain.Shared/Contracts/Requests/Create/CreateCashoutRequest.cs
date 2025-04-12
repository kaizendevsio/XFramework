namespace Payments.Domain.Shared.Contracts.Requests.Create;

using TRequest = CreateCashInRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record CreateCashoutRequest : TransactionRequestBase,
    ICommand, 
    IStreamflowRequest<TRequest, TResponse>
{
    public PaymentGateway? PaymentGateway { get; set; }
}