using MediatR;
using StreamFlow.Domain.Generic.Contracts.Requests;
using StreamFlow.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.BusinessObjects;

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