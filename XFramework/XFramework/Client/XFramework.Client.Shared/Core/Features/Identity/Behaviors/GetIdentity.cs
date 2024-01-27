using IdentityServer.Integration.Drivers;
using XFramework.Domain.Generic.Contracts;

namespace XFramework.Client.Shared.Core.Features.Identity;

public partial class IdentityState
{
    public record GetIdentity(Guid Id) : StateAction;

    protected class GetIdentityHandler(
        IIdentityServerServiceWrapper identityServerServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<GetIdentity>(handlerServices, store)
    {
        private IdentityState CurrentState => Store.GetState<IdentityState>();

        public override async Task Handle(GetIdentity request, CancellationToken cancellationToken)
        {
            var response = await identityServerServiceWrapper.IdentityInformation.Get(request.Id);
            if (await HandleFailure(response, request)) return;
            
            CurrentState.Identity = response.Response;
            Store.SetState(CurrentState);
            
            await HandleSuccess(response, request, true);
        }
    }
}

