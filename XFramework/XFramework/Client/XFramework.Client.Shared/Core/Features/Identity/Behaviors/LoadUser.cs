using IdentityServer.Integration.Drivers;
using XFramework.Domain.Generic.Contracts;

namespace XFramework.Client.Shared.Core.Features.Identity;

public partial class IdentityState
{
    public record LoadUser(Guid CredentialId) : StateAction;

    protected class LoadUserHandler(
        IIdentityServerServiceWrapper identityServerServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<LoadUser>(handlerServices, store)
    {
        private IdentityState CurrentState => Store.GetState<IdentityState>();

        public override async Task Handle(LoadUser request, CancellationToken cancellationToken)
        {
            await ReportBusy();
            
            var responseCredential = await identityServerServiceWrapper.IdentityCredential.Get(
                id: request.CredentialId, 
                includeNavigations: true, 
                navigationDepth: 2);
            if (await HandleFailure(responseCredential, request)) return;
            
            CurrentState.Credential = responseCredential.Response;
            CurrentState.Identity = responseCredential.Response.IdentityInfo;
            CurrentState.Contacts = responseCredential.Response.IdentityContacts.ToList();
            CurrentState.Roles = responseCredential.Response.IdentityRoles.ToList();
            
            store.SetState(CurrentState);
            
            await HandleSuccess(responseCredential, request, true);
            await ReportTaskCompleted();
        }
    }
}