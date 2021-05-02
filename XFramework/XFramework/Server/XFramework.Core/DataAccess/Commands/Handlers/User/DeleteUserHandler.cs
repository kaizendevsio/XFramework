using System.Net;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Domain.Generic.Contracts.Requests;
using Mapster;
using MediatR;
using XFramework.Core.DataAccess.Commands.Entity.User;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Core.DataAccess.Commands.Handlers.User
{
    public class DeleteUserHandler : CommandBaseHandler, IRequestHandler<DeleteUserCmd,CmdResponseBO<DeleteUserCmd>>
    {
        public DeleteUserHandler(IIdentityServiceWrapper identityServiceWrapper)
        {
            IdentityServiceWrapper = identityServiceWrapper;
        }
        
        public async Task<CmdResponseBO<DeleteUserCmd>> Handle(DeleteUserCmd request, CancellationToken cancellationToken)
        {
            var response = await IdentityServiceWrapper.UpdateIdentity(request.Adapt<UpdateIdentityRequest>());
            if (response.HttpStatusCode != HttpStatusCode.Accepted)
            {
                return new()
                {
                    HttpStatusCode = response.HttpStatusCode,
                    Message = response.Message
                };
            }
            
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted
            };
        }
    }
}