using System;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;

namespace Records.Api.SignalR.Handlers
{
    public class CreateIdentityHandler : ISignalREventHandler
    {
        public void Handle(HubConnection connection, IMediator mediator)
        {
            //throw new NotImplementedException();
        }
    }
}