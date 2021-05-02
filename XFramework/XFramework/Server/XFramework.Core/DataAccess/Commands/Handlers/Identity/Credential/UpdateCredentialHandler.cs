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
    public class UpdateCredentialHandler : CommandBaseHandler, IRequestHandler<UpdateCredentialCmd, CmdResponseBO<UpdateCredentialCmd>>
    {
        public UpdateCredentialHandler(IIdentityServiceWrapper identityServiceWrapper)
        {
            IdentityServiceWrapper = identityServiceWrapper;
        }
        
        public async Task<CmdResponseBO<UpdateCredentialCmd>> Handle(UpdateCredentialCmd request, CancellationToken cancellationToken)
        {
            var response = await IdentityServiceWrapper.UpdateCredential(request.Adapt<UpdateCredentialRequest>());
            return response.Adapt<CmdResponseBO<UpdateCredentialCmd>>();
        }
    }
}