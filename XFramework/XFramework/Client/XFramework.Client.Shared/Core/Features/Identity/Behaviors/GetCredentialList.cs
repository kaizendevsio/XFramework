using IdentityServer.Integration.Drivers;
using XFramework.Domain.Generic.Contracts;

namespace XFramework.Client.Shared.Core.Features.Identity;

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

        public override async Task Handle(GetCredentialList request, CancellationToken cancellationToken)
        {
            await ReportBusy();

            var filters = new List<QueryFilter>();
            
            filters.AddRange(request.SearchFields.Select(i => new QueryFilter
            {
                PropertyName = i,
                Operation = QueryFilterOperation.Contains,
                Value = request.SearchText
            }).ToList());
            
            var response = await identityServerServiceWrapper.IdentityCredential.GetList(
                pageSize: request.PageSize, 
                pageNumber: request.PageIndex, 
                includeNavigations: true,
                includes: new List<string>
                {
                    $"{nameof(IdentityCredential.IdentityInfo)}",
                    $"{nameof(IdentityCredential.IdentityContacts)}.{nameof(IdentityContact.Type)}",
                    $"{nameof(IdentityCredential.IdentityRoles)}.{nameof(IdentityRole.Type)}"
                },
                filter: filters);
            
            if (await HandleFailure(response, request)) return;
            
            CurrentState.CredentialList = response.Response;
            Store.SetState(CurrentState);
            
            await HandleSuccess(response, request, true);
            await ReportTaskCompleted();
        }
    }
}

