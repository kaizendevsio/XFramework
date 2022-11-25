
namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity.Credential;

public class UpdateCredentialHandler : CommandBaseHandler, IRequestHandler<UpdateCredentialCmd, CmdResponse<UpdateCredentialCmd>>
{
    public UpdateCredentialHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse<UpdateCredentialCmd>> Handle(UpdateCredentialCmd request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.IdentityCredentials
            .Where(i => i.Guid == request.Guid.ToString())
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            return new CmdResponse<UpdateCredentialCmd>
            {
                Message = $"Identity with data [GUID: {request.Guid}, Username: {request.UserName}] does not exist",
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