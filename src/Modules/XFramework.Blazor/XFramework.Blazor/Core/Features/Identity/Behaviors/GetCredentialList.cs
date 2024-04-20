using IdentityServer.Integration.Drivers;
using XFramework.Domain.Shared.Contracts;

namespace XFramework.Blazor.Core.Features.Identity;

public partial class IdentityState
{
    public record GetCredentialList() : QueryAction;

    protected class GetCredentialListHandler(
        IIdentityServerServiceWrapper identityServerServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<GetCredentialList>(handlerServices, store)
    {
        private IdentityState CurrentState => Store.GetState<IdentityState>();

        public override async Task Handle(GetCredentialList action, CancellationToken cancellationToken)
        {
            await ReportBusy();

            var filters = new List<QueryFilter>();
            
            filters.AddRange(action.SearchFields.Select(i => new QueryFilter
            {
                PropertyName = i,
                Operation = QueryFilterOperation.Contains,
                Value = action.SearchText
            }).ToList());
            
            var response = await identityServerServiceWrapper.IdentityCredential.GetList(
                pageSize: action.PageSize, 
                pageNumber: action.PageIndex, 
                includeNavigations: true,
                includes: new List<string>
                {
                    $"{nameof(IdentityCredential.IdentityInfo)}",
                    $"{nameof(IdentityCredential.IdentityContacts)}.{nameof(IdentityContact.Type)}",
                    $"{nameof(IdentityCredential.IdentityRoles)}.{nameof(IdentityRole.Type)}"
                },
                filter: filters);
            
            if (await HandleFailure(response, action)) return;
            
            CurrentState.CredentialList = response.Response;
            Store.SetState(CurrentState);
            
            await HandleSuccess(response, action, true);
            await ReportTaskCompleted();
        }
    }
}

