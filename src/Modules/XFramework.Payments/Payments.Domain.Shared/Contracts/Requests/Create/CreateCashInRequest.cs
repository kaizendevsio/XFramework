namespace Payments.Domain.Shared.Contracts.Requests.Create;

using TRequest = CreateCashInRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record CreateCashInRequest : Contracts.TransactionRequestBase,
    ICommand, 
    IStreamflowRequest<TRequest, TResponse>
{
    public PaymentGateway? PaymentGateway { get; set; }
    public string? SourceAccountNumber { get; set; }
    public string? SourceAccountName { get; set; }
    public string? PaymentMethod { get; set; }
}