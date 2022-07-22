namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Vendor;

public class UpdateVendorHandler : CommandBaseHandler, IRequestHandler<UpdateVendorCmd, CmdResponse<UpdateVendorCmd>>
{
    public UpdateVendorHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateVendorCmd>> Handle(UpdateVendorCmd request, CancellationToken cancellationToken)
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
        var updatedVendor = request.Adapt(existingVendor);

        if (request.EntityGuid is null)
        {
            var entity = await _dataLayer.HealthEssentialsContext.VendorEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.EntityGuid}", CancellationToken.None);
            if (entity is null)
            {
                return new ()
                {
                    Message = $"Entity with Guid {request.EntityGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedVendor.Entity = entity;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedVendor);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Vendor Entity with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}