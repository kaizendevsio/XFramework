namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Vendor;

public class UpdateVendorEntityGroupHandler : CommandBaseHandler, IRequestHandler<UpdateVendorEntityGroupCmd, CmdResponse<UpdateVendorEntityGroupCmd>>
{
    public UpdateVendorEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateVendorEntityGroupCmd>> Handle(UpdateVendorEntityGroupCmd request, CancellationToken cancellationToken)
    {
        var existingGroup = await _dataLayer.HealthEssentialsContext.VendorEntityGroups.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingGroup is null)
        {
            return new ()
            {
                Message = $"Entity Group with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedGroup = request.Adapt(existingGroup);

        _dataLayer.HealthEssentialsContext.Update(updatedGroup);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Entity Group with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}