﻿using Messaging.Core.DataAccess.Commands.Entity.Message;
using Messaging.Domain.Generic.Contracts.Requests.Update;

namespace Messaging.Api.SignalR.Handlers.Message.Update;

public class UpdateMessageThreadHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdateMessageThreadRequest, UpdateMessageThreadCmd>(connection, mediator);
    }
}