using System;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Domain.Generic.Contracts.Requests;
using IdentityServer.Domain.Generic.Contracts.Responses;
using Mapster;
using MediatR;
using XFramework.Core.DataAccess.Commands.Entity.Identity;
using XFramework.Core.DataAccess.Query.Entity.Identity;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Core.DataAccess.Query.Handlers.Identity
{
    public class AuthenticateIdentityHandler : QueryBaseHandler, IRequestHandler<AuthenticateIdentityQuery, QueryResponseBO<AuthorizeIdentityContract>>
    {
        public AuthenticateIdentityHandler(IIdentityServiceWrapper identityServiceWrapper)
        {
            IdentityServiceWrapper = identityServiceWrapper;
        }
        
        public async Task<QueryResponseBO<AuthorizeIdentityContract>> Handle(AuthenticateIdentityQuery request, CancellationToken cancellationToken)
        {
            var response = await IdentityServiceWrapper.Authenticate(request.Adapt<AuthenticateCredentialRequest>());
            return response.Adapt<QueryResponseBO<AuthorizeIdentityContract>>();
        }
    }
}
