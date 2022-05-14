using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Contacts;

namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity.Contacts;

public class DeleteContactHandler : CommandBaseHandler, IRequestHandler<DeleteContactCmd,CmdResponse>
{
    public DeleteContactHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
        
    public async Task<CmdResponse> Handle(DeleteContactCmd request, CancellationToken cancellationToken)
    {
        var credentialEntity = await _dataLayer.IdentityCredentials
            .AsNoTracking()
            .Where(i => i.Guid == $"{request.Guid}")
            .FirstOrDefaultAsync(cancellationToken);

        if (credentialEntity == null)
        {
            return new()
            {
                Message = $"Identity with data [GUID: {request.Guid}] does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
            
        var contactEntity = await _dataLayer.IdentityContactEntities
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
            
        var entity = await _dataLayer.IdentityContacts
            .Where(i => i.UserCredentialId == credentialEntity.Id)
            .Where(i => i.EntityId == contactEntity.Id)
            .Where(i => i.Guid == $"{request.Guid}")
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            return new ()
            {
                Message = $"Identity with data [Cuid: {request.Guid}, Cid: {request.Guid}] does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        entity.IsDeleted = true;
        entity.IsEnabled = false;
            
        _dataLayer.IdentityContacts.Update(entity);
        await _dataLayer.SaveChangesAsync(cancellationToken);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}