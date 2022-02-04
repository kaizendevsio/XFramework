
namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity.Credential;

public class UpdateCredentialHandler : CommandBaseHandler, IRequestHandler<UpdateCredentialCmd, CmdResponseBO<UpdateCredentialCmd>>
{
    public UpdateCredentialHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponseBO<UpdateCredentialCmd>> Handle(UpdateCredentialCmd request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.TblIdentityCredentials
            .Where(i => i.IdentityInfo.Guid == request.Guid.ToString())
            .Where(i => i.Guid == request.Guid.ToString())
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            return new CmdResponseBO<UpdateCredentialCmd>
            {
                Message = $"Identity with data [UID: {request.Guid}, Username: {request.UserName}] does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        entity = request.Adapt(entity);
        _dataLayer.Update(entity);
        await _dataLayer.SaveChangesAsync(cancellationToken);
            
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}