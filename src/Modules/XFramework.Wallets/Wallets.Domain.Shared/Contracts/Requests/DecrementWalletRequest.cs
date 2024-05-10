namespace Wallets.Domain.Shared.Contracts.Requests;

using TRequest = DecrementWalletRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record DecrementWalletRequest : TransactionRequestBase,
    IRequest<TResponse>,
    IStreamflowRequest<TRequest, TResponse>
{
    public Guid WalletId { get; set; }
    public Guid WalletTypeId { get; set; }
}