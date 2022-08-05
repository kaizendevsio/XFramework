using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;
using XFramework.Domain.Generic.Enums;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class UpdatePharmacyHandler : CommandBaseHandler, IRequestHandler<UpdatePharmacyCmd, CmdResponse<UpdatePharmacyCmd>>
{
    public UpdatePharmacyHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdatePharmacyCmd>> Handle(UpdatePharmacyCmd request, CancellationToken cancellationToken)
    {
        var existingPharmacy = await _dataLayer.HealthEssentialsContext.Pharmacies.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken: cancellationToken);
        if (existingPharmacy is null)
        {
            return new ()
            {
                Message = $"Pharmacy with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedPharmacy = request.Adapt(existingPharmacy);

        _dataLayer.HealthEssentialsContext.Update(updatedPharmacy);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Pharmacy with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}