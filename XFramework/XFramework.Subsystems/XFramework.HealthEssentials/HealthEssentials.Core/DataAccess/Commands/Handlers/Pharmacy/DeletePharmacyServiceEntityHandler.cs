using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class DeletePharmacyServiceEntityHandler : CommandBaseHandler, IRequestHandler<DeletePharmacyServiceEntityCmd, CmdResponse<DeletePharmacyServiceEntityCmd>>
{
    public DeletePharmacyServiceEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeletePharmacyServiceEntityCmd>> Handle(DeletePharmacyServiceEntityCmd request, CancellationToken cancellationToken)
    {
        var existingEntity = await _dataLayer.HealthEssentialsContext.PharmacyServiceEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingEntity is null)
        {
            return new ()
            {
                Message = $"Pharmacy Service Entity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingEntity.IsDeleted = true;
        existingEntity.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingEntity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Pharmacy Service Entity with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}