using System.Security.AccessControl;
using HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;
using HealthEssentials.Domain.Generics.Contracts;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Medicine;

public class CreateMedicineVendorHandler : CommandBaseHandler, IRequestHandler<CreateMedicineVendorCmd, CmdResponse<CreateMedicineVendorCmd>>
{
    public CreateMedicineVendorHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateMedicineVendorCmd>> Handle(CreateMedicineVendorCmd request, CancellationToken cancellationToken)
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
        
        var vendor = await _dataLayer.HealthEssentialsContext.Vendors.FirstOrDefaultAsync(x => x.Guid == $"{request.VendorGuid}", CancellationToken.None);
        if (vendor is null)
        {
            return new()
            {
                Message = $"Vendor with Guid {request.VendorGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var medicineVendor = request.Adapt<MedicineVendor>();
        medicineVendor.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        medicineVendor.Medicine = medicine;
        medicineVendor.Vendor = vendor;
        
        await _dataLayer.HealthEssentialsContext.MedicineVendors.AddAsync(medicineVendor, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(medicineVendor.Guid);
        return new()
        {
            Message = $"Medicine Vendor with Guid {medicineVendor.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };

        
    }
}