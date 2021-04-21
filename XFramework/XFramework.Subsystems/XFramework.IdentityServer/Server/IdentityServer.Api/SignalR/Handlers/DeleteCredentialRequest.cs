using MediatR;
using Microsoft.AspNetCore.SignalR.Client;

namespace IdentityServer.Api.SignalR.Handlers
{
    public class DeleteCredentialRequest : BaseSignalRHandler, ISignalREventHandler
    {
        public void Handle(HubConnection connection, IMediator mediator)
        {
            throw new System.NotImplementedException();
        }
    }
}