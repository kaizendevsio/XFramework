using IdentityServer.Integration.Drivers;
using XFramework.Domain.Shared.Contracts;

namespace XFramework.Blazor.Core.Features.Identity;

public partial class IdentityState
{
    public record QueryUser(Guid CredentialId) : StateAction<IdentityCredential?>;

    protected class QueryUserHandler(
        IIdentityServerServiceWrapper identityServerServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<QueryUser, IdentityCredential?>(handlerServices, store)
    {
        private IdentityState CurrentState => Store.GetState<IdentityState>();

        public override async Task<IdentityCredential?> Handle(QueryUser request, CancellationToken cancellationToken)
        {
            var responseCredential = await identityServerServiceWrapper.IdentityCredential.Get(
                id: request.CredentialId,
                includeNavigations: true,
                includes: new List<string>
                {
                    $"{nameof(IdentityCredential.IdentityInfo)}",
                    $"{nameof(IdentityCredential.IdentityContacts)}.{nameof(IdentityContact.Type)}",
                    $"{nameof(IdentityCredential.IdentityRoles)}.{nameof(IdentityRole.Type)}"
                });
            
           
            if (await HandleFailure(responseCredential, request)) return null;
            
            await HandleSuccess(responseCredential, request, true);

            var result = responseCredential.Response;
            result.IdentityInfo = responseCredential.Response.IdentityInfo;
            result.IdentityContacts = responseCredential.Response.IdentityContacts.ToList();
            
            return result;
        }
    }
}