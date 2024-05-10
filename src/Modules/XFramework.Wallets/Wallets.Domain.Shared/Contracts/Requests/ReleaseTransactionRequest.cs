namespace Wallets.Domain.Shared.Contracts.Requests;

using TRequest = ReleaseTransactionRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record ReleaseTransactionRequest : RequestBase, 
    IRequest<TResponse>,
    IStreamflowRequest<TRequest, TResponse>
{
    public required Guid Id { get; set; }
}