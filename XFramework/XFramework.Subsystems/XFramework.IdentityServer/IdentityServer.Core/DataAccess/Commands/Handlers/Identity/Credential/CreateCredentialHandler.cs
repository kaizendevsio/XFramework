using System;
using System.Linq;
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
    public class CreateCredentialHandler : CommandBaseHandler, IRequestHandler<CreateCredentialCmd, CmdResponseBO<CreateCredentialCmd>>
    {

        public CreateCredentialHandler(IDataLayer dataLayer)
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

            var anyCredential = _dataLayer.TblIdentityCredentials
                .AsNoTracking()
                .Any(i => i.UserName == request.UserName);
            
            if (anyCredential)
            {
                return new()
                {
                    Message = $"Username '{request.UserName}' already exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            var hashPasswordByte = Encoding.ASCII.GetBytes(BCrypt.Net.BCrypt.HashPassword(inputKey: request.PasswordString, workFactor:11));

            entity.Cuid = request.Cuid != null ? request.Cuid.ToString() : Guid.NewGuid().ToString();
            entity.PasswordByte = hashPasswordByte;
            entity.IdentityInfoId = identityInfo.Id;
            entity.ApplicationId = request.RequestServer.ApplicationId;

            await _dataLayer.TblIdentityCredentials.AddAsync(entity, cancellationToken);
            
            var roleEntity = await _dataLayer.TblIdentityRoleEntities
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == (long)request.RoleEntity, cancellationToken: cancellationToken);

            if (roleEntity == null)
            {
                return new ()
                {
                    Message = $"Role {request.RoleEntity} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }

            var role = new TblIdentityRole()
            {
                UserCred = entity,
                RoleEntityId = roleEntity.Id
            };

            await _dataLayer.TblIdentityRoles.AddAsync(role, cancellationToken);
            await _dataLayer.SaveChangesAsync(cancellationToken);

            return new ()
            {
                HttpStatusCode = HttpStatusCode.Accepted
            };
        }
    }
}