using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Ailment;

public class DeleteAilmentHandler : CommandBaseHandler, IRequestHandler<DeleteAilmentCmd, CmdResponse<DeleteAilmentCmd>>
{
    public DeleteAilmentHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteAilmentCmd>> Handle(DeleteAilmentCmd request, CancellationToken cancellationToken)
    {
        var existingAilment = await _dataLayer.HealthEssentialsContext.Ailments
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (existingAilment is null)
        {
            return new ()
            {
                Message = $"Ailment with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        existingAilment.IsDeleted = true;
        existingAilment.IsEnabled = false;
        
        _dataLayer.HealthEssentialsContext.Update(existingAilment);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Ailment with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}