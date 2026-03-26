namespace Messaging.Domain.Shared.Contracts.Requests.Threads;

using TRequest = AddThreadMemberRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record AddThreadMemberRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ThreadId { get; set; }
    public Guid CredentialId { get; set; }
}
