using XFramework.Core.DataAccess.Query.Entity.Identity;

namespace XFramework.Core.DataAccess.Query.Handlers.Identity
{
    public class GetIdentityHandler : QueryBaseHandler, IRequestHandler<GetIdentityQuery, QueryResponse<IdentityResponse>>
    {
        public GetIdentityHandler(IIdentityServiceWrapper identityServiceWrapper)
        {
            IdentityServiceWrapper = identityServiceWrapper;
        }
        public async Task<QueryResponse<IdentityResponse>> Handle(GetIdentityQuery request, CancellationToken cancellationToken)
        {
            var response = await IdentityServiceWrapper.GetIdentity(request.Adapt<GetIdentityRequest>());
            return response.Adapt<QueryResponse<IdentityResponse>>();
        }
    }
}