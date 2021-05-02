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
    public class UpdateIdentityHandler : CommandBaseHandler, IRequestHandler<UpdateIdentityCmd,CmdResponseBO<UpdateIdentityCmd>>
    {
        public UpdateIdentityHandler(IIdentityServiceWrapper identityServiceWrapper)
        {
            IdentityServiceWrapper = identityServiceWrapper;
        }
        
        public async Task<CmdResponseBO<UpdateIdentityCmd>> Handle(UpdateIdentityCmd request, CancellationToken cancellationToken)
        {
            var response = await IdentityServiceWrapper.UpdateIdentity(request.Adapt<UpdateIdentityRequest>());
            return response.Adapt<CmdResponseBO<UpdateIdentityCmd>>();
        }
    }
}