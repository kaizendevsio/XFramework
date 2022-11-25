﻿using Messaging.Core.DataAccess.Commands.Entity.Message;
using Messaging.Domain.Generic.Contracts.Requests.Create;

namespace Messaging.Api.SignalR.Handlers.Message.Create;

public class CreateMessageThreadMemberGroupHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreateMessageThreadMemberGroupRequest, CreateMessageThreadMemberGroupCmd>(connection, mediator);
    }
}