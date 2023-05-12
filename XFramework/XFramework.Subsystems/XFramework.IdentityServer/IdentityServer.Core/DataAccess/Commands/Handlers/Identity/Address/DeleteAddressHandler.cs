using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Address;

namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity.Address;

public class DeleteAddressHandler : CommandBaseHandler ,IRequestHandler<DeleteAddressCmd, CmdResponse<DeleteAddressCmd>>
{
    public DeleteAddressHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteAddressCmd>> Handle(DeleteAddressCmd request, CancellationToken cancellationToken)
    {
        var existingRecord = await _dataLayer.IdentityAddresses.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken);
        if (existingRecord == null)
        {
            return new ()
            {
                Message = $"Address with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        _dataLayer.IdentityAddresses.Remove(existingRecord);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = request
        };
    }
}