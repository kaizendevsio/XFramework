namespace Wallets.Domain.Shared.Contracts.Requests;

using TRequest = IncrementWalletRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record IncrementWalletRequest : TransactionRequestBase, 
    IRequest<TResponse>,
    IStreamflowRequest<TRequest, TResponse>
{
    public Guid WalletId { get; set; }
    public Guid WalletTypeId { get; set; }
}