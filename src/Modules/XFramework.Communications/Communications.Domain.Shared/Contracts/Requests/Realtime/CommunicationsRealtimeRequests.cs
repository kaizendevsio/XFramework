using Communications.Domain.Shared.Contracts.Realtime;

namespace Communications.Domain.Shared.Contracts.Requests.Realtime;

[MemoryPackable]
public partial record PublishCommunicationsTypingRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<PublishCommunicationsTypingRequest, CmdResponse>
{
    public Guid ThreadId { get; set; }
    public bool IsTyping { get; set; }
    // Appended for MemoryPack compatibility; an older caller's request reads as plain typing.
    public CommunicationsTypingActivity Activity { get; set; }
    public int Count { get; set; }
}

[MemoryPackable]
public partial record PublishCommunicationsPresenceRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<PublishCommunicationsPresenceRequest, CmdResponse>
{
    public bool IsOnline { get; set; }
}
