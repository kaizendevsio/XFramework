using System.Net;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Authorization;
using IdentityServer.Domain.BusinessObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity.Authorization
{
    public class DeleteAuthorizeIdentityHandler : CommandBaseHandler, IRequestHandler<DeleteAuthorizeIdentityCmd, CmdResponseBO<DeleteAuthorizeIdentityCmd>>
    {
        public async Task<CmdResponseBO<DeleteAuthorizeIdentityCmd>> Handle(DeleteAuthorizeIdentityCmd request, CancellationToken cancellationToken)
        {
            var entity = await _dataLayer.TblIdentityInformations.FirstOrDefaultAsync(i => i.Uuid == request.Uid.ToString(), cancellationToken: cancellationToken);
            
            if (entity == null)
            {
                return new CmdResponseBO<DeleteAuthorizeIdentityCmd>()
                {
                    Message = $"Identity with UID {request.Uid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            
            var result = await _dataLayer.TblIdentityCredentials.FirstOrDefaultAsync(i => i.UserName == request.Username, cancellationToken: cancellationToken);

            _dataLayer.Remove(result);
            await _dataLayer.SaveChangesAsync(cancellationToken);

            return new();
        }
    }
}