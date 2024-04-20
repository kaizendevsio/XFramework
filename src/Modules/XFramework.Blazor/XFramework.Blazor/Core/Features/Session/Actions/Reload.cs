using IdentityServer.Integration.Drivers;
using XFramework.Domain.Shared.Contracts;

namespace XFramework.Blazor.Core.Features.Session;

public partial class SessionState
{
    public record Reload : StateAction;

    protected class ReloadHandler(
        IIdentityServerServiceWrapper identityServerServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<Reload>(handlerServices, store)
    {
        private SessionState CurrentState => Store.GetState<SessionState>();
        
        public override async Task Handle(Reload action, CancellationToken aCancellationToken)
        {
            ReportBusy("Reloading user data...");
            
            var identityResponse = await identityServerServiceWrapper.IdentityCredential.Get(
                id: CurrentState.Credential.Id,
                includeNavigations: true,
                includes:[
                    $"{nameof(IdentityCredential.IdentityInfo)}.{nameof(IdentityInformation.IdentityAddresses)}.{nameof(IdentityAddress.AddressType)}",
                    $"{nameof(IdentityCredential.IdentityInfo)}.{nameof(IdentityInformation.IdentityAddresses)}.{nameof(IdentityAddress.Country)}",
                    $"{nameof(IdentityCredential.IdentityInfo)}.{nameof(IdentityInformation.IdentityAddresses)}.{nameof(IdentityAddress.Region)}",
                    $"{nameof(IdentityCredential.IdentityInfo)}.{nameof(IdentityInformation.IdentityAddresses)}.{nameof(IdentityAddress.Province)}",
                    $"{nameof(IdentityCredential.IdentityInfo)}.{nameof(IdentityInformation.IdentityAddresses)}.{nameof(IdentityAddress.City)}",
                    $"{nameof(IdentityCredential.IdentityInfo)}.{nameof(IdentityInformation.IdentityAddresses)}.{nameof(IdentityAddress.Barangay)}",
                    $"{nameof(IdentityCredential.IdentityContacts)}.{nameof(IdentityContact.Type)}",
                ]
                );
            
            if(await HandleFailure(identityResponse, action)) return;
            
            await Mediator.Send(new SetState()
            {
                Identity = identityResponse.Response.IdentityInfo,
                Credential = identityResponse.Response,
                ContactList = identityResponse.Response.IdentityContacts.ToList()
            });
            
            await HandleSuccess(action, "User data reloaded successfully.");
        }
    }
}