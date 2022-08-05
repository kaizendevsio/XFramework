namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Vendor;

public class DeleteVendorEntityGroupHandler : CommandBaseHandler, IRequestHandler<DeleteVendorEntityGroupCmd, CmdResponse<DeleteVendorEntityGroupCmd>>
{
    public DeleteVendorEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteVendorEntityGroupCmd>> Handle(DeleteVendorEntityGroupCmd request, CancellationToken cancellationToken)
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
        
        existingGroup.IsDeleted = true;
        existingGroup.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingGroup);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Entity Group with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}