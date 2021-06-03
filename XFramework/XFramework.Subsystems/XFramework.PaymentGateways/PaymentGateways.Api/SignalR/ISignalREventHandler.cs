using MediatR;
using Microsoft.AspNetCore.SignalR.Client;

namespace PaymentGateways.Api.SignalR
{
    public interface ISignalREventHandler
    {
        public void Handle(HubConnection connection, IMediator mediator);
    }
}