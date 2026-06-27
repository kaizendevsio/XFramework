namespace Messaging.Domain.Shared.Contracts.Requests.Realtime;

[MemoryPackable]
public partial record PublishMessagingTypingRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<PublishMessagingTypingRequest, CmdResponse>
{
    public Guid ThreadId { get; set; }
    public bool IsTyping { get; set; }
}

[MemoryPackable]
public partial record PublishMessagingPresenceRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<PublishMessagingPresenceRequest, CmdResponse>
{
    public bool IsOnline { get; set; }
}
