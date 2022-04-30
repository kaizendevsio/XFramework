namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity;

public class DeleteIdentityHandler : CommandBaseHandler ,IRequestHandler<DeleteIdentityCmd, CmdResponse<DeleteIdentityCmd>>
{
    public DeleteIdentityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse<DeleteIdentityCmd>> Handle(DeleteIdentityCmd request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.IdentityInformations.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken);
        if (entity == null)
        {
            return new CmdResponse<DeleteIdentityCmd>
            {
                Message = $"Identity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        if (_dataLayer.IdentityCredentials.Any(i => i.IdentityInfoId == entity.Id))
        {
            return new CmdResponse<DeleteIdentityCmd>
            {
                Message = $"Identity with Guid {request.Guid} has existing credentials and cannot be deleted",
                HttpStatusCode = HttpStatusCode.Forbidden
            };
        }
            
        _dataLayer.Remove(entity);
        await _dataLayer.SaveChangesAsync(cancellationToken);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted
        };

    }
}