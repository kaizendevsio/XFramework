using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class CreatePharmacyStockHandler : CommandBaseHandler, IRequestHandler<CreatePharmacyStockCmd, CmdResponse<CreatePharmacyStockCmd>>
{
    public CreatePharmacyStockHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreatePharmacyStockCmd>> Handle(CreatePharmacyStockCmd request, CancellationToken cancellationToken)
    {
        var pharmacy = await _dataLayer.HealthEssentialsContext.Pharmacies.FirstOrDefaultAsync(x => x.Guid == $"{request.PharmacyGuid}", CancellationToken.None);
        if (pharmacy is null)
        {
            return new ()
            {
                Message = $"Pharmacy with Guid {request.PharmacyGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var medicine = await _dataLayer.HealthEssentialsContext.Medicines.FirstOrDefaultAsync(x => x.Guid == $"{request.MedicineGuid}", CancellationToken.None);
        if (medicine is null)
        {
            return new ()
            {
                Message = $"Medicine with Guid {request.MedicineGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var pharmacyStock = request.Adapt<PharmacyStock>();
        pharmacyStock.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        pharmacyStock.Pharmacy = pharmacy;
        pharmacyStock.Medicine = medicine;

        await _dataLayer.HealthEssentialsContext.PharmacyStocks.AddAsync(pharmacyStock, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(pharmacyStock.Guid);
        return new()
        {
            Message = $"Pharmacy Stock with Guid {pharmacyStock.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}