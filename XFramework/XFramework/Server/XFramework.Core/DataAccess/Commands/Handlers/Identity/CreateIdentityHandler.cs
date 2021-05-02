using System.Net;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Domain.Generic.Contracts.Requests;
using Mapster;
using MediatR;
using XFramework.Core.DataAccess.Commands.Entity.Identity;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Core.DataAccess.Commands.Handlers.Identity
{
    public class CreateIdentityHandler : CommandBaseHandler, IRequestHandler<CreateIdentityCmd, CmdResponseBO<CreateIdentityCmd>>
    {
        public CreateIdentityHandler(IIdentityServiceWrapper identityServiceWrapper)
        {
            IdentityServiceWrapper = identityServiceWrapper;
        }

        public async Task<CmdResponseBO<CreateIdentityCmd>> Handle(CreateIdentityCmd request, CancellationToken cancellationToken)
        {
            var response = await IdentityServiceWrapper.CreateIdentity(request.Adapt<CreateIdentityRequest>());
            if (response.HttpStatusCode != HttpStatusCode.Accepted)
            {
                return response.Adapt<CmdResponseBO<CreateIdentityCmd>>();
            }

            response = await IdentityServiceWrapper.CreateCredential(request.Adapt<CreateCredentialRequest>());
            if (response.HttpStatusCode != HttpStatusCode.Accepted)
            {
                return response.Adapt<CmdResponseBO<CreateIdentityCmd>>();
            }

            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted
            };
        }
    }
}