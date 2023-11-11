namespace StreamFlow.Stream.Services.Entity.Events;

public class InvokeMethodQuery : CommandBaseEntity, IRequest<QueryResponse<StreamFlowInvokeResponse>>
{
    public InvokeMethodQuery()
    {
            
    }

    public InvokeMethodQuery(StreamFlowMessage messageQueue)
    {
        MessageQueue = messageQueue;
    }
        
    public StreamFlowMessage MessageQueue { get; set; }
}