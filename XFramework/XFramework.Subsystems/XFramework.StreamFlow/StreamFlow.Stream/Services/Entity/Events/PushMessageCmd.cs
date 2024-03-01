namespace StreamFlow.Stream.Services.Entity.Events;

public class PushMessageCmd : CommandBaseEntity, IRequest<CmdResponse<PushMessageCmd>>
{
    public PushMessageCmd()
    {
            
    }
    public StreamFlowMessage ? Message { get; set; }
}