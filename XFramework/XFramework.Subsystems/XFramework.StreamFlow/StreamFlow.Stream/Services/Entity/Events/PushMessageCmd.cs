using MediatR;
using Microsoft.AspNetCore.SignalR;
using StreamFlow.Core.DataAccess.Commands.Entity;
using StreamFlow.Domain.BusinessObjects;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Domain.Generic.Contracts.Requests;
using XFramework.Domain.Generic.BusinessObjects;

namespace StreamFlow.Stream.Services.Entity.Events;

public class PushMessageCmd : CommandBaseEntity, IRequest<CmdResponse<PushMessageCmd>>
{
    public PushMessageCmd()
    {
            
    }

    public PushMessageCmd(StreamFlowMessageBO messageQueue)
    {
        MessageQueue = messageQueue;
    }
    public StreamFlowMessageBO MessageQueue { get; set; }
}