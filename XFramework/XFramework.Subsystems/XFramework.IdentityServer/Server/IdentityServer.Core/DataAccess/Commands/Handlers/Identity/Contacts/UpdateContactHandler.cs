using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Contacts;
using IdentityServer.Core.Interfaces;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Generic.BusinessObjects;

namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity.Contacts
{
    public class UpdateContactHandler : CommandBaseHandler, IRequestHandler<UpdateContactCmd,CmdResponseBO<UpdateContactCmd>>
    {
        public UpdateContactHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        
        public async Task<CmdResponseBO<UpdateContactCmd>> Handle(UpdateContactCmd request, CancellationToken cancellationToken)
        {
            var credentialEntity = await _dataLayer.TblIdentityCredentials
                .AsNoTracking()
                .Where(i => i.Cuid == request.Cuid.ToString())
                .FirstOrDefaultAsync(cancellationToken);

            if (credentialEntity == null)
            {
                return new()
                {
                    Message = $"Identity with data [UID: {request.Cuid}] does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            
            var contactEntity = await _dataLayer.TblIdentityContactEntities
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == (long)request.ContactType ,cancellationToken);
            if (contactEntity == null)
            {
                return new ()
                {
                    Message = $"Contact entity with ID {request.ContactType} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            
            var entity = await _dataLayer.TblIdentityContacts
                .Where(i => i.UserCredentialId == credentialEntity.Id)
                .Where(i => i.UcentitiesId == contactEntity.Id)
                .Where(i => i.Id == request.Cid)
                .FirstOrDefaultAsync(cancellationToken);

            if (entity == null)
            {
                return new ()
                {
                    Message = $"Identity with data [Cuid: {request.Cuid}, Cid: {request.Cid}] does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }

            entity.Value = request.Value;
            _dataLayer.TblIdentityContacts.Update(entity);
            await _dataLayer.SaveChangesAsync(cancellationToken);

            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted
            };
        }
    }
}