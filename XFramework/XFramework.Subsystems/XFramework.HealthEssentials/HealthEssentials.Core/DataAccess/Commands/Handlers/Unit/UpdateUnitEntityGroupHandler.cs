using HealthEssentials.Core.DataAccess.Commands.Entity.Unit;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Unit;

public class UpdateUnitEntityGroupHandler : CommandBaseHandler, IRequestHandler<UpdateUnitEntityGroupCmd, CmdResponse<UpdateUnitEntityGroupCmd>>
{
    public UpdateUnitEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateUnitEntityGroupCmd>> Handle(UpdateUnitEntityGroupCmd request, CancellationToken cancellationToken)
    {
        var existingGroup = await _dataLayer.HealthEssentialsContext.UnitEntityGroups.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingGroup == null)
        {
            return new()
            {
                Message = $"Unit Entity Group with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedGroup = request.Adapt(existingGroup);

        _dataLayer.HealthEssentialsContext.Update(updatedGroup);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Entity Group with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
        
    }
}