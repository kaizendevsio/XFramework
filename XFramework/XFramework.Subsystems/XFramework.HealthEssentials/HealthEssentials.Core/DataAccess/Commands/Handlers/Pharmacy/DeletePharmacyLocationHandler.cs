using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class DeletePharmacyLocationHandler : CommandBaseHandler, IRequestHandler<DeletePharmacyLocationCmd, CmdResponse<DeletePharmacyLocationCmd>>
{
    public DeletePharmacyLocationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeletePharmacyLocationCmd>> Handle(DeletePharmacyLocationCmd request, CancellationToken cancellationToken)
    {
        var existingPharmacyLocation = await _dataLayer.HealthEssentialsContext.PharmacyLocations
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);

        if (existingPharmacyLocation == null)
        {
            return new ()
            {
                Message = $"Pharmacy Location with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingPharmacyLocation.IsDeleted = true;
        existingPharmacyLocation.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingPharmacyLocation);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Pharmacy Location with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}