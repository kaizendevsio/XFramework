namespace Communications.Domain.Shared.Contracts.Requests.Threads;

using TRequest = CreateThreadRequest;
using TResponse = QueryResponse<CreateThreadResponse>;

[MemoryPackable]
public partial record CreateThreadRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid TypeId { get; set; }
    public List<Guid> InitialMemberCredentialIds { get; set; } = [];
}
