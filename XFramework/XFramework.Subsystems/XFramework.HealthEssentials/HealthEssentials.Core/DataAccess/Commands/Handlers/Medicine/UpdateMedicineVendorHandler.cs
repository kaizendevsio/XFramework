using HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Medicine;

public class UpdateMedicineVendorHandler : CommandBaseHandler, IRequestHandler<UpdateMedicineVendorCmd, CmdResponse<UpdateMedicineVendorCmd>>
{
    public UpdateMedicineVendorHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateMedicineVendorCmd>> Handle(UpdateMedicineVendorCmd request, CancellationToken cancellationToken)
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
        var updatedVendor = request.Adapt(existingVendor);

        if (request.MedicineGuid is null)
        {
            var medicine = await _dataLayer.HealthEssentialsContext.Medicines.FirstOrDefaultAsync(x => x.Guid == $"{request.MedicineGuid}", CancellationToken.None);
            if (medicine is null)
            {
                return new()
                {
                    Message = $"Medicine with Guid {request.MedicineGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedVendor.Medicine = medicine;
        }

        if (request.VendorGuid is null)
        {
            var vendor = await _dataLayer.HealthEssentialsContext.Vendors.FirstOrDefaultAsync(x => x.Guid == $"{request.VendorGuid}", CancellationToken.None);
            if (vendor is null)
            {
                return new()
                {
                    Message = $"Vendor with Guid {request.VendorGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedVendor.Vendor = vendor;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedVendor);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Vendor with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}