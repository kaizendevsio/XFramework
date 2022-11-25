using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class DeletePharmacyStockHandler : CommandBaseHandler, IRequestHandler<DeletePharmacyStockCmd, CmdResponse<DeletePharmacyStockCmd>>
{
    public DeletePharmacyStockHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeletePharmacyStockCmd>> Handle(DeletePharmacyStockCmd request, CancellationToken cancellationToken)
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
        
        existingStock.IsDeleted = true;
        existingStock.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingStock);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Pharmacy Stock with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}