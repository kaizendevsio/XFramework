using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryServiceEntityGroupHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryServiceEntityGroupCmd, CmdResponse<DeleteLaboratoryServiceEntityGroupCmd>>
{
    public DeleteLaboratoryServiceEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse<DeleteLaboratoryServiceEntityGroupCmd>> Handle(DeleteLaboratoryServiceEntityGroupCmd request, CancellationToken cancellationToken)
    {
        var existingLaboratoryServiceEntityGroup = await _dataLayer.HealthEssentialsContext.LaboratoryServiceEntityGroups.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingLaboratoryServiceEntityGroup == null)
        {
            return new()
            {
                Message = $"Laboratory service entity group with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingLaboratoryServiceEntityGroup.IsDeleted = true;
        existingLaboratoryServiceEntityGroup.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingLaboratoryServiceEntityGroup);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Laboratory service entity group with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };

    }
}