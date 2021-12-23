using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Contacts;
using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Credential;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.DataTransferObjects;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Enums;
using XFramework.Integration.Services.Helpers;

namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity.Contacts
{
    public class CreateContactHandler : CommandBaseHandler, IRequestHandler<CreateContactCmd,CmdResponseBO<CreateContactCmd>>
    {
        public CreateContactHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }

        public async Task<CmdResponseBO<CreateContactCmd>> Handle(CreateContactCmd request, CancellationToken cancellationToken)
        {
            var identityCredential = await _dataLayer.TblIdentityCredentials
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Cuid == request.Cuid.ToString(), cancellationToken: cancellationToken);
            var entity = request.Adapt<TblIdentityCredential>();
            
            if (identityCredential == null)
            {
                return new ()
                {
                    Message = $"Identity with UID {request.Cuid} does not exist",
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
            
            var existingContact = _dataLayer.TblIdentityContacts.Any(i => i.Value == request.Value);
            if (existingContact)
            {
                return new ()
                {
                    Message = $"The {request.ContactType} '{request.Value}' already exist",
                    HttpStatusCode = HttpStatusCode.Conflict
                };
            }

            switch (request.ContactType)
            {
                case GenericContactType.NotSpecified:
                    break;
                case GenericContactType.Email:
                    request.Value.ValidateEmailAddress();
                    break;
                case GenericContactType.Phone:
                    request.Value = request.Value.ValidatePhoneNumber();
                    break;
            }
            
            var contact = new TblIdentityContact()
            {
                UserCredentialId = identityCredential.Id,
                UcentitiesId = contactEntity.Id,
                Value = request.Value
            };

            _dataLayer.TblIdentityContacts.Add(contact);
            await _dataLayer.SaveChangesAsync(cancellationToken);
            
            return new ()
            {
                HttpStatusCode = HttpStatusCode.Accepted
            };
        }
    }
}