using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Domain.Generic.Contracts.Requests;
using Mapster;
using MediatR;
using XFramework.Core.DataAccess.Commands.Entity.Identity.Credential;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Core.DataAccess.Commands.Handlers.Identity.Credential
{
    public class CreateCredentialHandler : CommandBaseHandler, IRequestHandler<CreateCredentialCmd, CmdResponseBO<CreateCredentialCmd>>
    {
        public CreateCredentialHandler(IIdentityServiceWrapper identityServiceWrapper)
        {
            IdentityServiceWrapper = identityServiceWrapper;
        }
        
        public async Task<CmdResponseBO<CreateCredentialCmd>> Handle(CreateCredentialCmd request, CancellationToken cancellationToken)
        {
            var response = await IdentityServiceWrapper.CreateIdentity(request.Adapt<CreateIdentityRequest>());
            return response.Adapt<CmdResponseBO<CreateCredentialCmd>>();
        }
    }
}