namespace Wallets.Domain.Shared.Contracts.Requests;

using TRequest = TransferWalletRequest;
using TResponse = CmdResponse;

public record TransferWalletRequest : TransactionRequestBase, 
    IRequest<TResponse>,
    IStreamflowRequest<TRequest, TResponse>
{
    public required Guid RecipientCredentialId { get; set; }
    public required Guid WalletTypeId { get; set; }
}