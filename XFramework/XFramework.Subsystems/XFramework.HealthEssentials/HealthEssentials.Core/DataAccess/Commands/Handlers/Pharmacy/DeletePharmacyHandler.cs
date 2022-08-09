using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class DeletePharmacyHandler : CommandBaseHandler, IRequestHandler<DeletePharmacyCmd, CmdResponse<DeletePharmacyCmd>>
{
    public DeletePharmacyHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeletePharmacyCmd>> Handle(DeletePharmacyCmd request, CancellationToken cancellationToken)
    {
        var existingPharmacy = await _dataLayer.HealthEssentialsContext.Pharmacies.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingPharmacy == null)
        {
            return new()
            {
                Message = $"Pharmacy with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingPharmacy.IsDeleted = true;
        existingPharmacy.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingPharmacy);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Pharmacy with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}