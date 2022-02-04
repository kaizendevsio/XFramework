﻿using IdentityServer.Domain.Generic.Contracts.Requests.Update;

namespace IdentityServer.Api.SignalR.Handlers;

public class UpdateCredentialHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<UpdateCredentialRequest, UpdateCredentialCmd>(connection, mediator);
    }
}