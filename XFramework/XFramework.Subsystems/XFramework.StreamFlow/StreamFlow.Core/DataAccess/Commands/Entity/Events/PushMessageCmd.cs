using MediatR;
using StreamFlow.Domain.BusinessObjects;
using XFramework.Domain.Generic.BusinessObjects;

namespace StreamFlow.Core.DataAccess.Commands.Entity.Events
{
    public class PushMessageCmd : CommandBaseEntity, IRequest<CmdResponseBO<PushMessageCmd>>
    {
        public StreamFlowMessageBO MessageQueue { get; set; }   
    }
}