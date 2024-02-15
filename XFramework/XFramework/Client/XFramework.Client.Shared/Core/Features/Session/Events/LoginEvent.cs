using IdentityServer.Domain.Generic.Contracts.Responses;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
        ILogger<LoginEventHandler> logger,
        IHostEnvironment hostEnvironment,
        IStore store)
        : EventHandler<LoginEvent>(handlerServices, store)
    {

        private SessionState CurrentState => Store.GetState<SessionState>();
        
        public override async Task Handle(LoginEvent action, CancellationToken cancellationToken)
        {
            if (action.StatusCode is HttpStatusCode.Accepted)
            {
                try
                {
                    await messageBusWrapper.StartClientEventListener($"{action.Data.Credential.Id}");
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Client event listener failed");
                }
            }
        }
    }
}