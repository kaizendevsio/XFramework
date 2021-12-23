using System.Net;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Credential;
using IdentityServer.Core.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Generic.BusinessObjects;

namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity.Credential
{
    public class DeleteCredentialHandler : CommandBaseHandler, IRequestHandler<DeleteCredentialCmd, CmdResponseBO<DeleteCredentialCmd>>
    {
        public DeleteCredentialHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        public async Task<CmdResponseBO<DeleteCredentialCmd>> Handle(DeleteCredentialCmd request, CancellationToken cancellationToken)
        {
            var entity = await _dataLayer.TblIdentityInformations.FirstOrDefaultAsync(i => i.Uuid == request.Uid.ToString(), cancellationToken: cancellationToken);
            
            if (entity == null)
            {
                return new CmdResponseBO<DeleteCredentialCmd>()
                {
                    Message = $"Identity with UID {request.Uid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            
            var result = await _dataLayer.TblIdentityCredentials.FirstOrDefaultAsync(i => i.UserName == request.Username, cancellationToken: cancellationToken);

            _dataLayer.Remove(result);
            await _dataLayer.SaveChangesAsync(cancellationToken);

            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted
            };
        }
    }
}