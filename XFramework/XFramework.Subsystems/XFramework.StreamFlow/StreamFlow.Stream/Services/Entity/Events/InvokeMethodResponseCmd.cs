using MediatR;
using StreamFlow.Domain.Generic.Contracts.Requests;
using StreamFlow.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.BusinessObjects;

namespace StreamFlow.Stream.Services.Entity.Events
{
    public class InvokeMethodResponseCmd : CommandBaseEntity, IRequest<CmdResponse<InvokeMethodResponseCmd>>
    {
        public InvokeMethodResponseCmd()
        {
            
        }

        public InvokeMethodResponseCmd(StreamFlowMessageBO messageQueue)
        {
            MessageQueue = messageQueue;
        }
        
        public StreamFlowMessageBO MessageQueue { get; set; }
    }
}