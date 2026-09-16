namespace Communications.Domain.Shared.Contracts.Requests.Threads;

/// <summary>Server-observed call metadata only; never audio or message plaintext.</summary>
[MemoryPackable]
public partial record RecordCallRequest : RequestBase, ICommand<CmdResponse>, IBoltRequest<RecordCallRequest, CmdResponse>
{
    public Guid CallId { get; set; }
    public Guid ThreadId { get; set; }
    public Guid CallerId { get; set; }
    public DateTimeOffset? ConnectedAt { get; set; }
    public DateTimeOffset EndedAt { get; set; }
}
