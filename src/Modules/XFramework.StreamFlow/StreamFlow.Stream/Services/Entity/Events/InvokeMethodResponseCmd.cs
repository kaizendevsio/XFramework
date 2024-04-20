namespace StreamFlow.Stream.Services.Entity.Events;

public class InvokeMethodResponseCmd : CommandBaseEntity, IRequest<CmdResponse<InvokeMethodResponseCmd>>
{
    public InvokeMethodResponseCmd()
    {
            
    }

    public InvokeMethodResponseCmd(StreamFlowMessage messageQueue)
    {
        MessageQueue = messageQueue;
    }
        
    public StreamFlowMessage MessageQueue { get; set; }
}