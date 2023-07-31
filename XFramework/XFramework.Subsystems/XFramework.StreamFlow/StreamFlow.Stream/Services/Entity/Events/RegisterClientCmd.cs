using MediatR;
using StreamFlow.Domain.BusinessObjects;
using StreamFlow.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.BusinessObjects;

namespace StreamFlow.Stream.Services.Entity.Events;

public class RegisterClientCmd : CommandBaseEntity, IRequest<CmdResponse<RegisterClientCmd>>
{
    public StreamFlowClientBO Client { get; set; }
}