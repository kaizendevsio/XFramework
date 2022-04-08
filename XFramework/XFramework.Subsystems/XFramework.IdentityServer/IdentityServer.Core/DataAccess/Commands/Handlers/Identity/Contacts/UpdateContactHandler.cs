using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Contacts;
using IdentityServer.Domain.DataTransferObjects.Legacy;

namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity.Contacts;

public class UpdateContactHandler : CommandBaseHandler, IRequestHandler<UpdateContactCmd,CmdResponse<UpdateContactCmd>>
{
    private readonly LegacyContext _legacyContext;

    public UpdateContactHandler(IDataLayer dataLayer, LegacyContext legacyContext)
    {
        _legacyContext = legacyContext;
        _dataLayer = dataLayer;
    }
        
    public async Task<CmdResponse<UpdateContactCmd>> Handle(UpdateContactCmd request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.TblIdentityContacts.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken: cancellationToken);
        if (entity == null)
        {
            return new ()
            {
                Message = $"Identity with data [Guid: '{request.Guid}'] does not exist",
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