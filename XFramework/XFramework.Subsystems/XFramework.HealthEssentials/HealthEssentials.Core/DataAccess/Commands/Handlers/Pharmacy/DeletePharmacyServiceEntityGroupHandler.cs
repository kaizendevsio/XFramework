using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class DeletePharmacyServiceEntityGroupHandler : CommandBaseHandler, IRequestHandler<DeletePharmacyServiceEntityGroupCmd, CmdResponse<DeletePharmacyServiceEntityGroupCmd>>
{
    public DeletePharmacyServiceEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeletePharmacyServiceEntityGroupCmd>> Handle(DeletePharmacyServiceEntityGroupCmd request, CancellationToken cancellationToken)
    {
        var existingGroup = await _dataLayer.HealthEssentialsContext.PharmacyServiceEntityGroups.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingGroup is null)
        {
            return new ()
            {
                Message = $"Pharmacy Service Entity Group with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingGroup.IsDeleted = true;
        existingGroup.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingGroup);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Pharmacy Service Entity Group with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}