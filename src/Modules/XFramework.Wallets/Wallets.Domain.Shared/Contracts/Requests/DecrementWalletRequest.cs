namespace Wallets.Domain.Shared.Contracts.Requests;

using TRequest = DecrementWalletRequest;
using TResponse = CmdResponse;

public record DecrementWalletRequest : TransactionRequestBase,
    IRequest<TResponse>,
    IStreamflowRequest<TRequest, TResponse>
{
    public required Guid WalletId { get; set; }
    public required Guid WalletTypeId { get; set; }
}