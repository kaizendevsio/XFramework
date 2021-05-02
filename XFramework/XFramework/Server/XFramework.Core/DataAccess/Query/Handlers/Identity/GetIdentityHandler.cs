using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Domain.Generic.Contracts.Requests;
using IdentityServer.Domain.Generic.Contracts.Responses;
using Mapster;
using MediatR;
using XFramework.Core.DataAccess.Query.Entity.Identity;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Core.DataAccess.Query.Handlers.Identity
{
    public class GetIdentityHandler : QueryBaseHandler, IRequestHandler<GetIdentityQuery, QueryResponseBO<GetIdentityContract>>
    {
        public GetIdentityHandler(IIdentityServiceWrapper identityServiceWrapper)
        {
            IdentityServiceWrapper = identityServiceWrapper;
        }
        public async Task<QueryResponseBO<GetIdentityContract>> Handle(GetIdentityQuery request, CancellationToken cancellationToken)
        {
            var response = await IdentityServiceWrapper.GetIdentity(request.Adapt<GetIdentityRequest>());
            return response.Adapt<QueryResponseBO<GetIdentityContract>>();
        }
    }
}