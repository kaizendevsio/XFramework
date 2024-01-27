using IdentityServer.Integration.Drivers;
using XFramework.Domain.Generic.Contracts;

namespace XFramework.Client.Shared.Core.Features.Identity;

public partial class IdentityState
{
    public record GetContacts(Guid Id) : QueryAction;

    protected class GetContactsHandler(
        IIdentityServerServiceWrapper identityServerServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<GetContacts>(handlerServices, store)
    {
        private IdentityState CurrentState => Store.GetState<IdentityState>();

        public override async Task Handle(GetContacts request, CancellationToken cancellationToken)
        {
            var filters = new List<QueryFilter>
            {
                new QueryFilter
                {
                    PropertyName = nameof(IdentityContact.CredentialId),
                    Operation = QueryFilterOperation.Equal,
                    Value = request.Id
                }
            };

            var response = await identityServerServiceWrapper.IdentityContact.GetList(
                pageSize: request.PageSize,
                pageNumber: request.PageIndex,
                filter: filters);
            
            if (await HandleFailure(response, request)) return;
            await HandleSuccess(response, request, true);
            
            CurrentState.Contacts = response.Response?.Items.ToList();
            Store.SetState(CurrentState);
        }
    }
}

