namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity.Credential;

public class DeleteCredentialHandler : CommandBaseHandler, IRequestHandler<DeleteCredentialCmd, CmdResponse<DeleteCredentialCmd>>
{
    public DeleteCredentialHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse<DeleteCredentialCmd>> Handle(DeleteCredentialCmd request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.TblIdentityInformations.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken: cancellationToken);
            
        if (entity == null)
        {
            return new CmdResponse<DeleteCredentialCmd>()
            {
                Message = $"Identity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
            
        var result = await _dataLayer.TblIdentityCredentials.FirstOrDefaultAsync(i => i.UserName == request.Username, cancellationToken: cancellationToken);

        _dataLayer.Remove(result);
        await _dataLayer.SaveChangesAsync(cancellationToken);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}