using IdentityServer.Integration.Drivers;

namespace XFramework.Blazor.Core.Features.Identity;

public partial class IdentityState
{
    public record UpdateContacts : StateAction;

    protected class UpdateContactsHandler(
        IIdentityServerServiceWrapper identityServerServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<UpdateContacts>(handlerServices, store)
    {
        private IdentityState CurrentState => Store.GetState<IdentityState>();
        

        public override async Task Handle(UpdateContacts request, CancellationToken cancellationToken)
        {
            if (CurrentState.Contacts is null || CurrentState.Contacts?.Count == 0)
            {
                return;
            }
            
            foreach (var currentStateContact in CurrentState.Contacts)
            {
                var response = await identityServerServiceWrapper.IdentityContact.Patch(currentStateContact);
                if (await HandleFailure(response, request)) return;
            }
            
            await Mediator.Send(new GetContacts(Id: CurrentState.Contacts.First().CredentialId));
            await HandleSuccess(request, "Contacts updated successfully.");
        }
    }
}

