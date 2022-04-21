using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Contacts;

namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity.Contacts;

public class UpdateContactHandler : CommandBaseHandler, IRequestHandler<UpdateContactCmd,CmdResponse<UpdateContactCmd>>
{

    public UpdateContactHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
        
    public async Task<CmdResponse<UpdateContactCmd>> Handle(UpdateContactCmd request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.IdentityContacts.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken: cancellationToken);
        if (entity == null)
        {
            return new ()
            {
                Message = $"Identity with data [Guid: '{request.Guid}'] does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        entity.Value = request.Value;
        _dataLayer.IdentityContacts.Update(entity);
        await _dataLayer.SaveChangesAsync(cancellationToken);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}