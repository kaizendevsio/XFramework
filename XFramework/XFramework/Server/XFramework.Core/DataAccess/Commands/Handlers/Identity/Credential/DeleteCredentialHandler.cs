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
    public class DeleteCredentialHandler : CommandBaseHandler, IRequestHandler<DeleteCredentialCmd, CmdResponseBO<DeleteCredentialCmd>>
    {
        public DeleteCredentialHandler(IIdentityServiceWrapper identityServiceWrapper)
        {
            IdentityServiceWrapper = identityServiceWrapper;
        }
        
        public async Task<CmdResponseBO<DeleteCredentialCmd>> Handle(DeleteCredentialCmd request, CancellationToken cancellationToken)
        {
            var response = await IdentityServiceWrapper.DeleteCredential(request.Adapt<DeleteCredentialRequest>());
            return response.Adapt<CmdResponseBO<DeleteCredentialCmd>>();
        }
    }
}