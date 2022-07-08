using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Ailment;

public class UpdateAilmentEntityGroupHandler : CommandBaseHandler, IRequestHandler<UpdateAilmentEntityGroupCmd, CmdResponse<UpdateAilmentEntityGroupCmd>>
{
    public UpdateAilmentEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateAilmentEntityGroupCmd>> Handle(UpdateAilmentEntityGroupCmd request, CancellationToken cancellationToken)
    {
        var existingAilmentEntityGroup = await _dataLayer.HealthEssentialsContext.AilmentEntityGroups
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (existingAilmentEntityGroup is null)
        {
            return new ()
            {
                Message = $"Ailment Entity Group with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var updatedAilmentEntityGroup = request.Adapt(existingAilmentEntityGroup);
        
        _dataLayer.HealthEssentialsContext.AilmentEntityGroups.Update(updatedAilmentEntityGroup);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Ailment Entity Group with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}