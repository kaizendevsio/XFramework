using MediatR;
using StreamFlow.Domain.Generic.Contracts.Requests;
using XFramework.Domain.Generic.BusinessObjects;

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