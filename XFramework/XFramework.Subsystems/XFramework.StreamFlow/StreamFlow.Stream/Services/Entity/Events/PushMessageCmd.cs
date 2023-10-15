using MediatR;
using StreamFlow.Domain.Generic.Contracts.Requests;
using XFramework.Domain.Generic.BusinessObjects;

namespace StreamFlow.Stream.Services.Entity.Events;

public class PushMessageCmd : CommandBaseEntity, IRequest<CmdResponse<PushMessageCmd>>
{
    public PushMessageCmd()
    {
            
    }
    public StreamFlowMessage? Message { get; set; }
}