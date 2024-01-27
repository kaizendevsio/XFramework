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
                filter: filters);
            
            if (await HandleFailure(response, request)) return;
            await HandleSuccess(response, request, true);
            
            CurrentState.CredentialList = response.Response;
            Store.SetState(CurrentState);
        }
    }
}

