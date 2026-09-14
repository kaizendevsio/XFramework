namespace Communications.Domain.Shared.Contracts.Requests.Threads;

[MemoryPackable]
public partial record SetThreadActiveStatusRequest : RequestBase, ICommand<CmdResponse>,
    IBoltRequest<SetThreadActiveStatusRequest, CmdResponse>
{
    public Guid ThreadId { get; set; }
    public bool ShareActiveStatus { get; set; }
}
