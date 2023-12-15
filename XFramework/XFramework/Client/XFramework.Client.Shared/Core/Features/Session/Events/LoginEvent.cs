using IdentityServer.Domain.Generic.Contracts.Responses;
using XFramework.Client.Shared.Interfaces;
using XFramework.Integration.Abstractions.Wrappers;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class LoginEvent : IEvent
    {
        public HttpStatusCode StatusCode { get; set; }
        public AuthenticateIdentityResponse? Data { get; set; }
    }
    
    protected class LoginEventHandler
    (
        IMessageBusWrapper messageBusWrapper,
        HandlerServices handlerServices,
        IStore store)
        : EventHandler<LoginEvent>(handlerServices, store)
    {

        private SessionState CurrentState => Store.GetState<SessionState>();
        
        public override async Task Handle(LoginEvent action, CancellationToken cancellationToken)
        {
            if (action.StatusCode is HttpStatusCode.Accepted)
            {
                Console.WriteLine("Client event listener started");
                try
                {
                    await messageBusWrapper.StartClientEventListener($"{CurrentState.Credential.Id}");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
                Console.WriteLine("Client event listener success");
            }
        }
    }
}