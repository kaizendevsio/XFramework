using IdentityServer.Integration.Drivers;

namespace XFramework.Client.Shared.Core.Features.Identity;

public partial class IdentityState
{
    public record UpdateIdentity : StateAction;

    protected class UpdateIdentityHandler(
        IIdentityServerServiceWrapper identityServerServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<UpdateIdentity>(handlerServices, store)
    {
        private IdentityState CurrentState => Store.GetState<IdentityState>();
        
        public override async Task Handle(UpdateIdentity request, CancellationToken cancellationToken)
        {
            var response = await identityServerServiceWrapper.IdentityInformation.Patch(CurrentState.Identity);
            if (await HandleFailure(response, request)) return;
            
            CurrentState.Identity = response.Response;
            Store.SetState(CurrentState);
            
            await HandleSuccess(response, request, true);
        }
    }
}

