using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class DeletePharmacyServiceHandler : CommandBaseHandler, IRequestHandler<DeletePharmacyServiceCmd, CmdResponse<DeletePharmacyServiceCmd>>
{
    public DeletePharmacyServiceHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeletePharmacyServiceCmd>> Handle(DeletePharmacyServiceCmd request, CancellationToken cancellationToken)
    {
        var existingService = await _dataLayer.HealthEssentialsContext.PharmacyServices.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingService is null)
        {
            return new ()
            {
                Message = $"Pharmacy Service with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingService.IsDeleted = true;
        existingService.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingService);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Pharmacy Service with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}