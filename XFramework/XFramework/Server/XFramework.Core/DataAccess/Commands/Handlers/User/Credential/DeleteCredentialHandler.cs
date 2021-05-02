using System.Threading;
using System.Threading.Tasks;
using MediatR;
using XFramework.Core.DataAccess.Commands.Entity.User.Credential;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Core.DataAccess.Commands.Handlers.User.Credential
{
    public class DeleteCredentialHandler : CommandBaseHandler, IRequestHandler<DeleteCredentialCmd, CmdResponseBO<DeleteCredentialCmd>>
    {
        public DeleteCredentialHandler(IIdentityServiceWrapper identityServiceWrapper)
        {
            IdentityServiceWrapper = identityServiceWrapper;
        }
        
        public Task<CmdResponseBO<DeleteCredentialCmd>> Handle(DeleteCredentialCmd request, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }
    }
}