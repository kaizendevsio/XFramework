using XFramework.Core.DataAccess.Query.Entity.Identity;

namespace XFramework.Core.DataAccess.Query.Handlers.Identity
{
    public class GetIdentityHandler : QueryBaseHandler, IRequestHandler<GetIdentityQuery, QueryResponseBO<IdentityInfoResponse>>
    {
        public GetIdentityHandler(IIdentityServiceWrapper identityServiceWrapper)
        {
            IdentityServiceWrapper = identityServiceWrapper;
        }
        public async Task<QueryResponseBO<IdentityInfoResponse>> Handle(GetIdentityQuery request, CancellationToken cancellationToken)
        {
            var response = await IdentityServiceWrapper.GetIdentity(request.Adapt<GetIdentityRequest>());
            return response.Adapt<QueryResponseBO<IdentityInfoResponse>>();
        }
    }
}