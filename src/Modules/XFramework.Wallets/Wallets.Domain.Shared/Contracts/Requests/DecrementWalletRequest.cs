namespace Wallets.Domain.Shared.Contracts.Requests;

using TRequest = DecrementWalletRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record DecrementWalletRequest : TransactionRequestBase,
    ICommand<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid WalletId { get; set; }
    public Guid WalletTypeId { get; set; }
}