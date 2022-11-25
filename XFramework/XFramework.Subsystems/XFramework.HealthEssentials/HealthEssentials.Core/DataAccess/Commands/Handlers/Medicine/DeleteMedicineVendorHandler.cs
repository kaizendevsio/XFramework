using HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Medicine;

public class DeleteMedicineVendorHandler : CommandBaseHandler, IRequestHandler<DeleteMedicineVendorCmd, CmdResponse<DeleteMedicineVendorCmd>>
{
    public DeleteMedicineVendorHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteMedicineVendorCmd>> Handle(DeleteMedicineVendorCmd request, CancellationToken cancellationToken)
    {
        var existingVendor = await _dataLayer.HealthEssentialsContext.MedicineVendors.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingVendor is null)
        {
            return new ()
            {
                Message = $"Vendor with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingVendor.IsDeleted = true;
        existingVendor.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingVendor);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Vendor with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}