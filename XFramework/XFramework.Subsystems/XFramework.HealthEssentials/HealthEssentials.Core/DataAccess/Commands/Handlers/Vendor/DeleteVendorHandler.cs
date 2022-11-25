namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Vendor;

public class DeleteVendorHandler : CommandBaseHandler, IRequestHandler<DeleteVendorCmd, CmdResponse<DeleteVendorCmd>>
{
    public DeleteVendorHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteVendorCmd>> Handle(DeleteVendorCmd request, CancellationToken cancellationToken)
    {
        var existingVendor = await _dataLayer.HealthEssentialsContext.Vendors.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingVendor is null)
        {
            return new ()
            {
                Message = $"Vendor Entity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingVendor.IsDeleted = true;
        existingVendor.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingVendor);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Vendor Entity with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}