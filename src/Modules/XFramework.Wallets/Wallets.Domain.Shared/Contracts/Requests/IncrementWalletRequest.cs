namespace Wallets.Domain.Shared.Contracts.Requests;

using TRequest = IncrementWalletRequest;
using TResponse = CmdResponse;

public record IncrementWalletRequest : TransactionRequestBase, 
    IRequest<TResponse>,
    IStreamflowRequest<TRequest, TResponse>
{
    public Guid WalletId { get; set; }
    public Guid WalletTypeId { get; set; }
}