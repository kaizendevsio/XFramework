namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity;

public class UpdateIdentityHandler : CommandBaseHandler, IRequestHandler<UpdateIdentityCmd, CmdResponse<UpdateIdentityCmd>>
{
    public UpdateIdentityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse<UpdateIdentityCmd>> Handle(UpdateIdentityCmd request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.TblIdentityInformations.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken);
        if (entity == null)
        {
            return new CmdResponse<UpdateIdentityCmd>
            {
                Message = $"Identity with Guid {request.Guid} does not exist",
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