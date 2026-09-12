namespace Communications.Domain.Shared.Contracts.Requests.Threads;

[MemoryPackable]
public partial record DeleteThreadRequest : RequestBase, ICommand<CmdResponse>, IBoltRequest<DeleteThreadRequest, CmdResponse>
{
    public Guid ThreadId { get; set; }
}
