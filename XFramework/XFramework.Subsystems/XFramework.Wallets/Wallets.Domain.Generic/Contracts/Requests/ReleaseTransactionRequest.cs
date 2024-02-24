namespace Wallets.Domain.Generic.Contracts.Requests;

using TRequest = ReleaseTransactionRequest;
using TResponse = CmdResponse;

public record ReleaseTransactionRequest : RequestBase, 
    IRequest<TResponse>,
    IStreamflowRequest<TRequest, TResponse>
{
    public required Guid Id { get; set; }
}