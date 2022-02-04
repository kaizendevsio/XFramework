using XFramework.Core.DataAccess.Query.Entity.Identity;

namespace XFramework.Core.DataAccess.Query.Handlers.Identity
{
    public class AuthenticateIdentityHandler : QueryBaseHandler, IRequestHandler<AuthenticateIdentityQuery, QueryResponseBO<AuthorizeIdentityResponse>>
    {
        public AuthenticateIdentityHandler(IIdentityServiceWrapper identityServiceWrapper)
        {
            IdentityServiceWrapper = identityServiceWrapper;
        }
        
        public async Task<QueryResponseBO<AuthorizeIdentityResponse>> Handle(AuthenticateIdentityQuery request, CancellationToken cancellationToken)
        {
            var response = await IdentityServiceWrapper.AuthenticateCredential(request.Adapt<AuthenticateCredentialRequest>());
            return response.Adapt<QueryResponseBO<AuthorizeIdentityResponse>>();
        }
    }
}
