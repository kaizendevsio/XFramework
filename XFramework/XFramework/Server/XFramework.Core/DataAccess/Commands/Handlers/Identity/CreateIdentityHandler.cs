using System;
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
            var uuid = Guid.NewGuid();
            var req = request.Adapt<CreateIdentityRequest>();
            req.Uuid = uuid;
            
            var response = await IdentityServiceWrapper.CreateIdentity(req);
            if (response.HttpStatusCode != HttpStatusCode.Accepted)
            {
                return response.Adapt<CmdResponseBO<CreateIdentityCmd>>();
            }

            var req2 = request.Adapt<CreateCredentialRequest>();
            req2.Uid = uuid;
            
            var response2 = await IdentityServiceWrapper.CreateCredential(req2);
            if (response2.HttpStatusCode != HttpStatusCode.Accepted)
            {
                return response2.Adapt<CmdResponseBO<CreateIdentityCmd>>();
            }

            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted
            };
        }
    }
}