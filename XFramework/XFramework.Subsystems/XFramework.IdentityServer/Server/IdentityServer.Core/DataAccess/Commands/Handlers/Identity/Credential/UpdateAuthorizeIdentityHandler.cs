using System.Net;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Credential;
using IdentityServer.Core.Interfaces;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Generic.BusinessObjects;

namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity.Credential
{
    public class UpdateAuthorizeIdentityHandler : CommandBaseHandler, IRequestHandler<UpdateCredentialCmd, CmdResponseBO<UpdateCredentialCmd>>
    {
        public UpdateAuthorizeIdentityHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        public async Task<CmdResponseBO<UpdateCredentialCmd>> Handle(UpdateCredentialCmd request, CancellationToken cancellationToken)
        {
            var entity = await _dataLayer.TblIdentityCredentials.FirstOrDefaultAsync(i => i.IdentityInfo.Uuid == request.Uid.ToString() & i.UserName == request.UserName , cancellationToken);

            if (entity == null)
            {
                return new CmdResponseBO<UpdateCredentialCmd>
                {
                    Message = $"Identity with data [UID: {request.Uid}, Username: {request.UserName}] does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }

            entity = request.Adapt(entity);
            _dataLayer.Update(entity);
            await _dataLayer.SaveChangesAsync(cancellationToken);

            return new();
        }
    }
}