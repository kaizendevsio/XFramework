namespace Messaging.Domain.Shared.Contracts.Requests.Threads;

using TRequest = RemoveThreadMemberRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record RemoveThreadMemberRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ThreadId { get; set; }
    public Guid CredentialId { get; set; }
}
