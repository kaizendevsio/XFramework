using IdentityServer.Integration.Drivers;

namespace XFramework.Blazor.Core.Features.Identity;

public partial class IdentityState
{
    public record UpdateCredential : StateAction;
    
    protected class UpdateCredentialHandler(
        IIdentityServerServiceWrapper identityServerServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<UpdateCredential>(handlerServices, store)
    {
        private IdentityState CurrentState => Store.GetState<IdentityState>();
        
        public override async Task Handle(UpdateCredential request, CancellationToken cancellationToken)
        {
            var response = await identityServerServiceWrapper.IdentityCredential.Patch(CurrentState.Credential);
            if (await HandleFailure(response, request)) return;
            await HandleSuccess(response, request, true);
            
            CurrentState.Credential = response.Response;
            Store.SetState(CurrentState);
        }
    }
}

