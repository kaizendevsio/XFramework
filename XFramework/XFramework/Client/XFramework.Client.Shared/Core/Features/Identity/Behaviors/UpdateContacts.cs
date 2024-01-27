using IdentityServer.Integration.Drivers;
using XFramework.Domain.Generic.Contracts;

namespace XFramework.Client.Shared.Core.Features.Identity;

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

            HandleSuccess(request, "Contacts updated successfully.");

            await Mediator.Send(new GetContacts(Id: CurrentState.Contacts.First().CredentialId));
        }
    }
}

