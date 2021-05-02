using System.Threading;
using System.Threading.Tasks;
using MediatR;
using XFramework.Core.DataAccess.Commands.Entity.User.Credential;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Core.DataAccess.Commands.Handlers.User.Credential
{
    public class CreateCredentialHandler : CommandBaseHandler, IRequestHandler<CreateCredentialCmd, CmdResponseBO<CreateCredentialCmd>>
    {
        public CreateCredentialHandler(IIdentityServiceWrapper identityServiceWrapper)
        {
            IdentityServiceWrapper = identityServiceWrapper;
        }
        
        public Task<CmdResponseBO<CreateCredentialCmd>> Handle(CreateCredentialCmd request, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }
    }
}