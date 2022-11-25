using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class UpdateLaboratoryServiceEntityGroupHandler : CommandBaseHandler, IRequestHandler<UpdateLaboratoryServiceEntityGroupCmd, CmdResponse<UpdateLaboratoryServiceEntityGroupCmd>>
{
    public UpdateLaboratoryServiceEntityGroupHandler()
    {
        
    }
    public async Task<CmdResponse<UpdateLaboratoryServiceEntityGroupCmd>> Handle(UpdateLaboratoryServiceEntityGroupCmd request, CancellationToken cancellationToken)
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
        var updatedLaboratoryServiceEntityGroup = request.Adapt(existingLaboratoryServiceEntityGroup);
        
        _dataLayer.HealthEssentialsContext.Update(updatedLaboratoryServiceEntityGroup);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new ()
        {
            Message = $"Laboratory service entity group with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}