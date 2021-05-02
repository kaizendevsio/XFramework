using System.Threading;
using System.Threading.Tasks;
using MediatR;
using XFramework.Core.DataAccess.Commands.Entity.User.Credential;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Core.DataAccess.Commands.Handlers.User.Credential
{
    public class UpdateCredentialHandler : CommandBaseHandler, IRequestHandler<UpdateCredentialCmd, CmdResponseBO<UpdateCredentialCmd>>
    {
        public UpdateCredentialHandler(IIdentityServiceWrapper identityServiceWrapper)
        {
            IdentityServiceWrapper = identityServiceWrapper;
        }
        
        public Task<CmdResponseBO<UpdateCredentialCmd>> Handle(UpdateCredentialCmd request, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }
    }
}