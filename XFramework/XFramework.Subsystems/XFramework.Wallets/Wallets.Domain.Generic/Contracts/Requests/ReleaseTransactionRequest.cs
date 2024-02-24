namespace Wallets.Domain.Generic.Contracts.Requests;

using TRequest = ReleaseTransactionRequest;
using TResponse = CmdResponse;

public record ReleaseTransactionRequest : TransactionRequestBase, 
    IRequest<TResponse>,
    IStreamflowRequest<TRequest, TResponse>
{
    public required Guid RecipientCredentialId { get; set; }
    public required Guid WalletTypeId { get; set; }
}