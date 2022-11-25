using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class UpdateLaboratoryEntityGroupHandler : CommandBaseHandler, IRequestHandler<UpdateLaboratoryEntityGroupCmd, CmdResponse<UpdateLaboratoryEntityGroupCmd>>
{
    public UpdateLaboratoryEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateLaboratoryEntityGroupCmd>> Handle(UpdateLaboratoryEntityGroupCmd request, CancellationToken cancellationToken)
    {
        var existingGroup = await _dataLayer.HealthEssentialsContext.LaboratoryEntityGroups.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingGroup is null)
        {
            return new ()
            {
                Message = $"Laboratory Entity Group with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedGroup = request.Adapt(existingGroup);

        _dataLayer.HealthEssentialsContext.Update(updatedGroup);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Laboratory Entity Group with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };

    }
}