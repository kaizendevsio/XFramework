using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Credential;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.DataTransferObjects;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Generic.BusinessObjects;

namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity.Credential
{
    public class CreateAuthorizeIdentityHandler : CommandBaseHandler, IRequestHandler<CreateCredentialCmd, CmdResponseBO<CreateCredentialCmd>>
    {

        public CreateAuthorizeIdentityHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        public async Task<CmdResponseBO<CreateCredentialCmd>> Handle(CreateCredentialCmd request, CancellationToken cancellationToken)
        {
            var identityInfo = await _dataLayer.TblIdentityInformations.FirstOrDefaultAsync(i => i.Uuid == request.Uid.ToString(), cancellationToken: cancellationToken);
            var entity = request.Adapt<TblIdentityCredential>();
            
            if (identityInfo == null)
            {
                return new CmdResponseBO<CreateCredentialCmd>
                {
                    Message = $"Identity with UID {request.Uid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            
            SHA512 shaM = new SHA512Managed();
            var passwordByte = Encoding.ASCII.GetBytes(request.PasswordString);
            var hashPasswordByte = shaM.ComputeHash(passwordByte);

            entity.Cuid = Guid.NewGuid().ToString();
            entity.PasswordByte = hashPasswordByte;
            entity.IdentityInfoId = identityInfo.Id;
            entity.ApplicationId = request.RequestServer.ApplicationId;
            
            await _dataLayer.TblIdentityCredentials.AddAsync(entity, cancellationToken);
            await _dataLayer.SaveChangesAsync(cancellationToken);

            return new();
        }
    }
}