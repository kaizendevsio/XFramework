using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class UpdatePharmacyStockHandler : CommandBaseHandler, IRequestHandler<UpdatePharmacyStockCmd, CmdResponse<UpdatePharmacyStockCmd>>
{
    public UpdatePharmacyStockHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdatePharmacyStockCmd>> Handle(UpdatePharmacyStockCmd request, CancellationToken cancellationToken)
    {
        var existingStock = await _dataLayer.HealthEssentialsContext.PharmacyStocks.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingStock is null)
        {
            return new ()
            {
                Message = $"Pharmacy Stock with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedStock = request.Adapt(existingStock);

        if (request.PharmacyGuid is null)
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
            updatedStock.Pharmacy = pharmacy;
        }

        if (request.MedicineGuid is null)
        {
            var medicine = await _dataLayer.HealthEssentialsContext.Medicines.FirstOrDefaultAsync(x => x.Guid == $"{request.MedicineGuid}", CancellationToken.None);
            if (medicine is null)
            {
                return new ()
                {
                    Message = $"Medicine with Guid {request.MedicineGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedStock.Medicine = medicine;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedStock);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Pharmacy Stock with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };

    }
}