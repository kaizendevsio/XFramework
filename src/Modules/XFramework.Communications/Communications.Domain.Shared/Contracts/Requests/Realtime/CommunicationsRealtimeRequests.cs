namespace Communications.Domain.Shared.Contracts.Requests.Realtime;

[MemoryPackable]
public partial record PublishCommunicationsTypingRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<PublishCommunicationsTypingRequest, CmdResponse>
{
    public Guid ThreadId { get; set; }
    public bool IsTyping { get; set; }
}

[MemoryPackable]
public partial record PublishCommunicationsPresenceRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<PublishCommunicationsPresenceRequest, CmdResponse>
{
    public bool IsOnline { get; set; }
}
