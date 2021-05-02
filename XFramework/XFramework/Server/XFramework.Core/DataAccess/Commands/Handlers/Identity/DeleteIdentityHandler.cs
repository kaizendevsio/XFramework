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
    public class DeleteIdentityHandler : CommandBaseHandler, IRequestHandler<DeleteIdentityCmd,CmdResponseBO<DeleteIdentityCmd>>
    {
        public DeleteIdentityHandler(IIdentityServiceWrapper identityServiceWrapper)
        {
            IdentityServiceWrapper = identityServiceWrapper;
        }
        
        public async Task<CmdResponseBO<DeleteIdentityCmd>> Handle(DeleteIdentityCmd request, CancellationToken cancellationToken)
        {
            var response = await IdentityServiceWrapper.DeleteIdentity(request.Adapt<DeleteIdentityRequest>());
            return response.Adapt<CmdResponseBO<DeleteIdentityCmd>>();
           
        }
    }
}