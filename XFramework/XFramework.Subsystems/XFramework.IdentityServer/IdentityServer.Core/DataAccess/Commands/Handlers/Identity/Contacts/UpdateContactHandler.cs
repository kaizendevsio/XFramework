using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Contacts;
using IdentityServer.Domain.DataTransferObjects.Legacy;

namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity.Contacts;

public class UpdateContactHandler : CommandBaseHandler, IRequestHandler<UpdateContactCmd,CmdResponseBO<UpdateContactCmd>>
{
    private readonly LegacyContext _legacyContext;

    public UpdateContactHandler(IDataLayer dataLayer, LegacyContext legacyContext)
    {
        _legacyContext = legacyContext;
        _dataLayer = dataLayer;
    }
        
    public async Task<CmdResponseBO<UpdateContactCmd>> Handle(UpdateContactCmd request, CancellationToken cancellationToken)
    {
        var credentialEntity = await _dataLayer.TblIdentityCredentials
            .AsNoTracking()
            .Where(i => i.Guid == $"{request.CredentialGuid}")
            .FirstOrDefaultAsync(cancellationToken);

        if (credentialEntity == null)
        {
            return new()
            {
                Message = $"Identity with data [UID: {request.Guid}] does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
            
        var contactEntity = await _dataLayer.TblIdentityContactEntities
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
            .Where(i => i.Guid == $"{request.Guid}")
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            return new ()
            {
                Message = $"Identity with data [Guid: {request.Guid}, Cid: {request.CredentialGuid}] does not exist",
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