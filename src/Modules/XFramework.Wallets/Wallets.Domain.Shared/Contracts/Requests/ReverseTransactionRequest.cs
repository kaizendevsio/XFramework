namespace Wallets.Domain.Shared.Contracts.Requests;

using TRequest = ReverseTransactionRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record ReverseTransactionRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid TransactionId { get; set; }
    public Guid WalletTransferId { get; set; }
    public string? Reason { get; set; }
}
