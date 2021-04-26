using MediatR;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Domain.Generic.Contracts.Requests;
using StreamFlow.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.BusinessObjects;

namespace StreamFlow.Stream.Services.Entity.Events
{
    public class InvokeMethodQuery : CommandBaseEntity, IRequest<QueryResponseBO<StreamFlowInvokeResponse>>
    {
        public InvokeMethodQuery()
        {
            
        }

        public InvokeMethodQuery(StreamFlowMessageBO messageQueue)
        {
            MessageQueue = messageQueue;
        }
        
        public StreamFlowMessageBO MessageQueue { get; set; }
    }
}