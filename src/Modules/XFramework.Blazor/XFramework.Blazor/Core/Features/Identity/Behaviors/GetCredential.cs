using IdentityServer.Integration.Drivers;

namespace XFramework.Blazor.Core.Features.Identity;

public partial class IdentityState
{
    public record GetCredential(Guid Id) : StateAction;

    protected class GetCredentialHandler(
        IIdentityServerServiceWrapper identityServerServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<GetCredential>(handlerServices, store)
    {
        private IdentityState CurrentState => Store.GetState<IdentityState>();

        public override async Task Handle(GetCredential request, CancellationToken cancellationToken)
        {
            var response = await identityServerServiceWrapper.IdentityCredential.Get(request.Id);
            if (await HandleFailure(response, request)) return;
            
            CurrentState.Credential = response.Response;
            Store.SetState(CurrentState);
            
            await HandleSuccess(response, request, true);
        }
    }
}

