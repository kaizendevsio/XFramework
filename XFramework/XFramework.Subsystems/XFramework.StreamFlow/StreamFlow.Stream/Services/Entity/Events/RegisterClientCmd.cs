using MediatR;
using StreamFlow.Domain.BusinessObjects;
using XFramework.Domain.Generic.BusinessObjects;

namespace StreamFlow.Stream.Services.Entity.Events
{
    public class RegisterClientCmd : CommandBaseEntity, IRequest<CmdResponseBO<RegisterClientCmd>>
    {
        public StreamFlowClientBO Client { get; set; }
    }
}