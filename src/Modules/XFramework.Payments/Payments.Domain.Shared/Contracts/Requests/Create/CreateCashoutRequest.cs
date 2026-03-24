namespace Payments.Domain.Shared.Contracts.Requests.Create;

using TRequest = CreateCashInRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record CreateCashoutRequest : TransactionRequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public PaymentGateway? PaymentGateway { get; set; }
}