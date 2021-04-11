using MediatR;
using StreamFlow.Core.DataAccess.Commands.Entity;
using StreamFlow.Domain.BusinessObjects;
using XFramework.Domain.Generic.BusinessObjects;

namespace StreamFlow.Stream.Services.Entity.Events
{
    public class PushMessageCmd : CommandBaseEntity, IRequest<CmdResponseBO<PushMessageCmd>>
    {
        public StreamFlowMessageBO MessageQueue { get; set; }
    }
}