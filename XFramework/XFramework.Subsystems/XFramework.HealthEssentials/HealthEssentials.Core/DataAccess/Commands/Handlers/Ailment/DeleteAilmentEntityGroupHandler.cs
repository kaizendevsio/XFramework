using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Ailment;

public class DeleteAilmentEntityGroupHandler : CommandBaseHandler, IRequestHandler<DeleteAilmentEntityGroupCmd, CmdResponse<DeleteAilmentEntityGroupCmd>>
{
    public DeleteAilmentEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteAilmentEntityGroupCmd>> Handle(DeleteAilmentEntityGroupCmd request, CancellationToken cancellationToken)
    {
        var existingAilmentEntityGroup = await _dataLayer.HealthEssentialsContext.AilmentEntityGroups.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingAilmentEntityGroup is null)
        {
            return new ()
            {
                Message = $"Ailment Entity Group with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingAilmentEntityGroup.IsDeleted = true;
        existingAilmentEntityGroup.IsEnabled = false;
        
        _dataLayer.HealthEssentialsContext.Update(existingAilmentEntityGroup);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Ailment Entity Group with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}