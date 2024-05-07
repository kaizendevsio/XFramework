namespace Wallets.Domain.Shared.Contracts.Requests;

using TRequest = IncrementWalletRequest;
using TResponse = CmdResponse;

public record IncrementWalletRequest : TransactionRequestBase, 
    IRequest<TResponse>,
    IStreamflowRequest<TRequest, TResponse>
{
    public required Guid WalletId { get; set; }
    public required Guid WalletTypeId { get; set; }
}