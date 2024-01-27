using IdentityServer.Integration.Drivers;
using XFramework.Domain.Generic.Contracts;

namespace XFramework.Client.Shared.Core.Features.Identity;

public partial class IdentityState
{
    public record UpdateContact : StateAction;

    protected class UpdateContactHandler(
        IIdentityServerServiceWrapper identityServerServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<UpdateContact>(handlerServices, store)
    {
        private IdentityState CurrentState => Store.GetState<IdentityState>();
        

        public override async Task Handle(UpdateContact request, CancellationToken cancellationToken)
        {
            var response = await identityServerServiceWrapper.IdentityContact.Patch(CurrentState.SelectedContact);
            if (await HandleFailure(response, request)) return;
            await HandleSuccess(response, request, true);
            
            CurrentState.SelectedContact = response.Response;
            Store.SetState(CurrentState);
        }
    }
}

