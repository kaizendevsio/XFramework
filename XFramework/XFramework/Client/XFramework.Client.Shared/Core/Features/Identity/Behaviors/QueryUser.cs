using IdentityServer.Integration.Drivers;
using XFramework.Domain.Generic.Contracts;

namespace XFramework.Client.Shared.Core.Features.Identity;

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
            var responseCredential = await identityServerServiceWrapper.IdentityCredential.Get(request.CredentialId);
            if (await HandleFailure(responseCredential, request)) return null;
            
            var responseIdentity = await identityServerServiceWrapper.IdentityInformation.Get(responseCredential.Response.IdentityInfoId);
            if (await HandleFailure(responseIdentity, request)) return null;
            
            var filters = new List<QueryFilter>
            {
                new QueryFilter
                {
                    PropertyName = nameof(IdentityContact.CredentialId),
                    Operation = QueryFilterOperation.Equal,
                    Value = responseCredential.Response.Id
                }
            };

            var responseContact = await identityServerServiceWrapper.IdentityContact.GetList(
                pageSize: 100_000,
                pageNumber: 1,
                filter: filters);
            if (await HandleFailure(responseContact, request)) return null;
            
            await HandleSuccess(responseCredential, request, true);

            var result = responseCredential.Response;
            result.IdentityInfo = responseIdentity.Response;
            result.IdentityContacts = responseContact.Response?.Items?.ToList();
            
            return result;
        }
    }
}